// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LibreLancer.Missions;
using LibreLancer.Data;
using LibreLancer.Data.GameData.World;
using LibreLancer.Data.IO;
using LibreLancer.Data.Schema.Pilots;
using LibreLancer.Data.Schema.Save;
using LibreLancer.Database;
using LancerNexus.Protocol;
using LibreLancer.Net;
using LibreLancer.Net.Protocol;
using LibreLancer.Physics;
using LibreLancer.Resources;
using LibreLancer.World;
using Microsoft.EntityFrameworkCore.Design;
using MessagePack;

namespace LibreLancer.Server
{
    public class GameServer
    {
        public string ServerName = "Librelancer Server";
        public string ServerDescription = "Description of the server is here.";
    public string ServerNews = "News of the server goes here";
    public string? LoginUrl = null;
    public string? InstanceId { get; set; }
        public string? NpcCoordinatorUrl { get; set; }
        public string? NpcTransferStagingDirectory { get; set; }
        public int NpcTransferPort { get; set; }
        public System.Security.Cryptography.X509Certificates.X509Certificate2? NpcTransferCertificate { get; set; }
        public System.Security.Cryptography.X509Certificates.X509Certificate2? NpcTransferCaCertificate { get; set; }
    public string? SystemId { get; set; }
    public string[] SystemIds { get; set; } = [];
    public bool OwnsSystem(string system) =>
        SystemIds.Length > 0 ? SystemIds.Contains(system, StringComparer.OrdinalIgnoreCase) :
        string.Equals(SystemId, system, StringComparison.OrdinalIgnoreCase);
    public string? TransferInstanceKey { private get; set; }
    private LocalOperatorStore? localOperators;
        private string? npcCoordinatorApiKey;
        private readonly HttpClient npcTransferHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private readonly ConcurrentDictionary<string, NpcIdentityAllocator> npcIdentityAllocators = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, byte> npcTransferRestores = new();
        private readonly ConcurrentDictionary<Guid, byte> npcTransferCompleted = new();
        private readonly ConcurrentDictionary<Guid, byte> npcSourceTransferResolutions = new();
        private readonly NpcTransferGroupReservations populationNpcTransferReservations = new();
        private readonly Guid npcTransferActivationGeneration = Guid.NewGuid();

        private bool IsNpcTransferActivated(Guid transferId) =>
            !string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) &&
            NpcTransferActivationReceipt.IsRecorded(NpcTransferStagingDirectory, transferId,
                npcTransferActivationGeneration);

        public void ConfigureNpcIdentityAllocation(string? apiKey) => npcCoordinatorApiKey = apiKey;

        private readonly object npcRetirementSync = new();
        private NpcRetirementOutbox? npcRetirementOutbox;
        private NpcCheckpointOutbox? npcCheckpointOutbox;

        private NpcRetirementOutbox EnsureNpcRetirementOutbox()
        {
            lock (npcRetirementSync)
            {
                if (npcRetirementOutbox != null)
                    return npcRetirementOutbox;
                if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) ||
                    string.IsNullOrWhiteSpace(NpcCoordinatorUrl) || string.IsNullOrWhiteSpace(npcCoordinatorApiKey) ||
                    string.IsNullOrWhiteSpace(InstanceId))
                    throw new InvalidOperationException("NPC retirement requires configured staging and Coordinator identity.");
                npcRetirementOutbox = new NpcRetirementOutbox(
                    Path.Combine(NpcTransferStagingDirectory, "retirements"), new Uri(NpcCoordinatorUrl),
                    npcCoordinatorApiKey, InstanceId);
                npcCheckpointOutbox = new NpcCheckpointOutbox(
                    Path.Combine(NpcTransferStagingDirectory, "checkpoints"), new Uri(NpcCoordinatorUrl),
                    npcCoordinatorApiKey, InstanceId);
                return npcRetirementOutbox;
            }
        }

        public async Task InitializeNpcRetirementOutboxAsync()
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory)) return;
            var outbox = EnsureNpcRetirementOutbox();
            await Task.WhenAll(outbox.Ready, npcCheckpointOutbox!.Ready).ConfigureAwait(false);
        }

        internal bool IsNpcCheckpointOutboxReady => npcCheckpointOutbox?.Ready.IsCompletedSuccessfully == true;

        internal long GetNpcCheckpointRevision(Guid npcId, long ownershipVersion) =>
            npcCheckpointOutbox?.GetRevision(npcId, ownershipVersion) ?? 0;

        internal bool IsNpcCheckpointPending(Guid npcId) => npcCheckpointOutbox?.IsPending(npcId) == true;

        internal (Task Durable, Task<LancerNexus.Protocol.NpcCheckpointWriteResponseV1> Completed)
            QueueNpcCheckpoint(LancerNexus.Protocol.NpcCheckpointWriteRequestV1 request) =>
            (npcCheckpointOutbox ?? throw new InvalidOperationException("NPC checkpoint outbox is not initialized."))
                .Queue(request);

        internal async Task RestoreNpcCheckpointsForWorldAsync(ServerWorld world,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory)) return;
            await InitializeNpcRetirementOutboxAsync().ConfigureAwait(false);
            var outbox = npcCheckpointOutbox ?? throw new InvalidOperationException("NPC checkpoint outbox is not initialized.");
            await outbox.WaitForPendingWritesAsync(cancellationToken).ConfigureAwait(false);
            var systemId = world.System.Nickname.ToLowerInvariant();
            Guid? after = null;
            var seen = new HashSet<Guid>();
            while (true)
            {
                var page = await outbox.GetRecoveryPageAsync(systemId, after, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var checkpointId in page.CheckpointIds)
                {
                    if (!seen.Add(checkpointId))
                        throw new InvalidDataException("Coordinator repeated an NPC checkpoint across recovery pages.");
                    var recovery = await outbox.GetRecoveryCheckpointAsync(checkpointId, systemId,
                        cancellationToken).ConfigureAwait(false);
                    if (recovery is not null)
                        RestoreNpcCheckpoint(world, recovery);
                }
                if (page.NextAfterCheckpointId is null) break;
                after = page.NextAfterCheckpointId;
            }
        }

        private static void RestoreNpcCheckpoint(ServerWorld world, NpcCheckpointRecoveryV1 recovery)
        {
            var checkpoint = recovery.Snapshot;
            var snapshot = new NpcTransferSnapshot
            {
                TransferId = checkpoint.RequestId,
                NpcIds = checkpoint.Npcs.Select(npc => npc.NpcId).ToArray(),
                Npcs = checkpoint.Npcs,
                TargetSystemId = checkpoint.SystemId,
                Formations = checkpoint.Formations
            };
            NpcTransferContractValidator.Validate(snapshot);
            world.NPCs.RestoreTransfer(snapshot, incrementOwnershipVersion: false,
                expectedSystemId: world.System.Nickname.ToLowerInvariant());
            var memberIds = snapshot.NpcIds.ToHashSet();
            var members = world.GameWorld.Objects.Where(obj =>
                    obj.TryGetComponent<Components.SNPCComponent>(out var npc) && memberIds.Contains(npc.NpcId))
                .ToArray();
            if (members.Length != memberIds.Count)
                throw new InvalidDataException("NPC checkpoint recovery did not activate its complete member set.");
            world.Population.AdoptRecoveredCheckpointGroup(members);
            FLLog.Info("NPC Checkpoint", $"Restored checkpoint {checkpoint.RequestId:D} in {world.System.Nickname} ({members.Length} ships).");
        }

        internal bool TryQueueNpcTerminalCheckpoint(ServerWorld world, LibreLancer.World.GameObject terminal,
            LancerNexus.Protocol.NpcRetirementReasonV1 reason, Action accepted)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory)) return false;
            return world.TryQueueNpcTerminalCheckpoint(terminal, reason, accepted);
        }

        internal bool BlocksNpcRetirementActivation(Guid npcId, long version)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) || string.IsNullOrWhiteSpace(NpcCoordinatorUrl) ||
                string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
                return false;
            var outbox = EnsureNpcRetirementOutbox();
            if (!outbox.Ready.IsCompletedSuccessfully)
                throw new InvalidOperationException("NPC retirement recovery is not ready for activation.");
            return outbox.BlocksActivation(npcId, version);
        }

        public (Task Durable, Task<LancerNexus.Protocol.NpcRetirementResponseV1> Completed)
            QueueNpcRetirement(LancerNexus.Protocol.NpcRetirementEntryV1[] npcs) =>
            EnsureNpcRetirementOutbox().Queue(new LancerNexus.Protocol.NpcRetirementRequestV1
                { RequestId = Guid.NewGuid(), InstanceId = InstanceId!, Npcs = npcs });


        /// <summary>Starts a population ship or escort group transfer without blocking the simulation thread.</summary>
        public bool BeginPopulationNpcTransfer(GameObject lead, string targetSystemId, string targetArrivalObject,
            string sourceGate, ServerWorld sourceWorld)
        {
            var group = sourceWorld.Population.GatherPopulationTransferGroup(lead);
            if (group.Length == 0)
                return false;
            var transferId = Guid.NewGuid();
            var reservation = populationNpcTransferReservations.TryReserve(transferId,
                group.Select(npc => npc.NpcId));
            if (reservation == NpcTransferReservationResult.Conflict)
                return true;
            if (reservation == NpcTransferReservationResult.AlreadyReserved)
                return true;
            sourceWorld.RetainPopulationTransfer();
            _ = TransferPopulationNpcGroupAsync(transferId, group, sourceWorld, targetSystemId, targetArrivalObject,
                sourceGate);
            return true;
        }

        private async Task TransferPopulationNpcGroupAsync(Guid transferId, JumperNpc[] group, ServerWorld sourceWorld,
            string targetSystemId, string targetArrivalObject, string sourceGate)
        {
            try
            {
                if (OwnsSystem(targetSystemId))
                {
                    await TransferPopulationNpcGroupLocallyAsync(transferId, group, sourceWorld, targetSystemId,
                        targetArrivalObject).ConfigureAwait(false);
                    return;
                }
                if (string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(NpcCoordinatorUrl) ||
                    string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
                    throw new InvalidOperationException("Autonomous NPC transfer is not configured.");
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    new Uri(new Uri(NpcCoordinatorUrl), "/internal/v1/npc-transfers/target"))
                {
                    Content = JsonContent.Create(new NpcTransferTargetResolveRequestV1
                    {
                        SourceInstanceId = InstanceId,
                        TargetSystemId = targetSystemId
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
                using var response = await npcTransferHttp.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var target = await response.Content.ReadFromJsonAsync<NpcTransferTargetResolveResultV1>()
                    .ConfigureAwait(false);
                if (target is not { Found: true } || string.IsNullOrWhiteSpace(target.TargetInstanceId) ||
                    string.IsNullOrWhiteSpace(target.TargetEndpoint))
                    throw new InvalidOperationException("Coordinator has no ready transfer-capable target for the destination system.");

                await PrepareAndSendNpcTransferAsync(transferId, target.TargetInstanceId, target.TargetEndpoint,
                    targetSystemId, targetArrivalObject, 0, -1, group, sourceWorld, null).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                FLLog.Warning("NPC Transfer", $"Population group transfer {transferId:D} failed: {exception}");
                try
                {
                    await AbortNpcTransferAndRestoreAsync(transferId, sourceWorld, null).ConfigureAwait(false);
                    await ResumeFailedPopulationNpcGroupAsync(group, sourceWorld, sourceGate).ConfigureAwait(false);
                }
                catch (Exception restoreException)
                {
                    FLLog.Error("NPC Transfer", $"Population group recovery {transferId:D} is pending: {restoreException}");
                }
            }
            finally
            {
                populationNpcTransferReservations.Release(transferId);
                sourceWorld.ReleasePopulationTransfer();
            }
        }

        private static async Task ResumeFailedPopulationNpcGroupAsync(JumperNpc[] group, ServerWorld sourceWorld,
            string failedGate)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            sourceWorld.EnqueueAction(() =>
            {
                try
                {
                    sourceWorld.Population.ResumeFailedTraderGroup(group.Select(npc => npc.NpcId).ToArray(), failedGate);
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
            });
            await completion.Task.ConfigureAwait(false);
        }

        private async Task TransferPopulationNpcGroupLocallyAsync(Guid transferId, JumperNpc[] group,
            ServerWorld sourceWorld, string targetSystemId, string targetArrivalObject)
        {
            NpcTransferSnapshot? snapshot = null;
            await sourceWorld.FreezeJumpersAsync(group, null, triggerMissionExit: false,
                beforeRemove: captured => snapshot = BuildNpcTransferSnapshot(transferId, targetSystemId, null,
                    captured, targetArrivalObject)).ConfigureAwait(false);
            var frozen = snapshot ?? throw new InvalidDataException("Local population jump did not capture a snapshot.");
            try
            {
                var targetWorld = await RequestNpcWorldAsync(targetSystemId, CancellationToken.None).ConfigureAwait(false);
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                targetWorld.EnqueueAction(() =>
                {
                    try
                    {
                        var incoming = RebaseNpcSnapshotAtArrival(frozen, targetWorld);
                        var restored = targetWorld.NPCs.RestoreTransfer(incoming, incrementOwnershipVersion: false,
                            expectedSystemId: targetSystemId);
                        targetWorld.Population.AdoptTransferredTraderGroup(restored, targetArrivalObject);
                        completion.TrySetResult();
                    }
                    catch (Exception exception) { completion.TrySetException(exception); }
                });
                await completion.Task.ConfigureAwait(false);
            }
            catch
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                sourceWorld.EnqueueAction(() =>
                {
                    try
                    {
                        sourceWorld.NPCs.RestoreTransfer(frozen, incrementOwnershipVersion: false,
                            expectedSystemId: sourceWorld.System.Nickname, emitMissionSystemTransition: false);
                        completion.TrySetResult();
                    }
                    catch (Exception exception) { completion.TrySetException(exception); }
                });
                await completion.Task.ConfigureAwait(false);
                throw;
            }
        }

        public void MarkNpcTransferActivated(Guid transferId)
        {
            var directory = NpcTransferStagingDirectory
                ?? throw new InvalidOperationException("NPC transfer staging is not configured.");
            NpcTransferActivationReceipt.Record(directory, transferId, npcTransferActivationGeneration);
        }

        /// <summary>Durably stages an authenticated incoming NPC snapshot without activating simulation.</summary>
        public async ValueTask<NpcPeerSnapshotTransferAck> StageIncomingNpcTransferAsync(
            NpcPeerSnapshotTransfer request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) ||
                !string.Equals(request.TargetInstanceId, InstanceId, StringComparison.Ordinal))
                return new NpcPeerSnapshotTransferAck { TransferId = request.TransferId, ReasonCode = "target_not_configured" };
            ValidateIncomingNpcTransferSnapshot(request);
            var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(request.SnapshotMessagePack,
                MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            if (!string.IsNullOrWhiteSpace(snapshot.TargetArrivalObject))
            {
                var targetWorld = await RequestNpcWorldAsync(snapshot.TargetSystemId, cancellationToken).ConfigureAwait(false);
                var arrivalExists = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                targetWorld.EnqueueAction(() => arrivalExists.TrySetResult(
                    targetWorld.GameWorld.GetObject(snapshot.TargetArrivalObject) is not null));
                if (!await arrivalExists.Task.WaitAsync(cancellationToken).ConfigureAwait(false))
                    return new NpcPeerSnapshotTransferAck
                    {
                        TransferId = request.TransferId,
                        ReasonCode = "target_arrival_object_missing"
                    };
            }
            if (IsNpcTransferActivated(request.TransferId))
                return new NpcPeerSnapshotTransferAck
                {
                    TransferId = request.TransferId,
                    Accepted = true,
                    ReasonCode = "already_activated"
                };

            Directory.CreateDirectory(NpcTransferStagingDirectory);
            var finalPath = Path.Combine(NpcTransferStagingDirectory, $"{request.TransferId:N}.msgpack");
            if (File.Exists(finalPath))
            {
                var existing = await File.ReadAllBytesAsync(finalPath, cancellationToken);
                var duplicate = CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(request.SnapshotMessagePack));
                return new NpcPeerSnapshotTransferAck
                {
                    TransferId = request.TransferId,
                    Accepted = duplicate,
                    ReasonCode = duplicate ? "already_staged" : "staged_snapshot_conflict"
                };
            }

            var tempPath = Path.Combine(NpcTransferStagingDirectory, $".{request.TransferId:N}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await output.WriteAsync(request.SnapshotMessagePack, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                    output.Flush(flushToDisk: true);
                }
                try { File.Move(tempPath, finalPath); }
                catch (IOException) when (File.Exists(finalPath))
                {
                    var existing = await File.ReadAllBytesAsync(finalPath, cancellationToken);
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(request.SnapshotMessagePack)))
                        return new NpcPeerSnapshotTransferAck { TransferId = request.TransferId, ReasonCode = "staged_snapshot_conflict" };
                }
                return new NpcPeerSnapshotTransferAck { TransferId = request.TransferId, Accepted = true, ReasonCode = "staged_inert" };
            }
            finally
            {
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }

        private void ValidateIncomingNpcTransferSnapshot(NpcPeerSnapshotTransfer request)
        {
            var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(request.SnapshotMessagePack,
                MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.TransferId != request.TransferId ||
                !OwnsSystem(snapshot.TargetSystemId) || GameData.Items.Systems.Get(snapshot.TargetSystemId) is null)
                throw new InvalidDataException("NPC transfer target does not match this instance's owned systems.");

            NpcMissionRuntimeStateV1? missionState = null;
            if (snapshot.MissionRuntimeState.Length > 0)
            {
                missionState = MessagePackSerializer.Deserialize<NpcMissionRuntimeStateV1>(
                    snapshot.MissionRuntimeState,
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            }
            if (snapshot.MissionRuntimeId.HasValue != (missionState is not null))
                throw new InvalidDataException("NPC mission runtime identity and state must be transferred together.");

            long? missionCharacterId = null;
            foreach (var identity in snapshot.Npcs)
            {
                var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(identity.RuntimeState,
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                if (string.IsNullOrWhiteSpace(state.Nickname) || string.IsNullOrWhiteSpace(state.DisplayName))
                    throw new InvalidDataException("NPC transfer is missing a restorable nickname or display name.");
                if (GameData.Items.Ships.Get(state.LoadoutArchetype) is null)
                    throw new InvalidDataException($"Target is missing NPC ship archetype '{state.LoadoutArchetype}'.");
                foreach (var equipment in state.Equipment)
                {
                    if (GameData.Items.Equipment.Get(equipment.EquipmentId) is null)
                        throw new InvalidDataException($"Target is missing NPC equipment '{equipment.EquipmentId}'.");
                    if (equipment.ExtensionData.Length > 0)
                    {
                        var extension = JsonSerializer.Deserialize<NpcEquipmentTransferExtensionV1>(equipment.ExtensionData);
                        if (extension is not { SchemaVersion: 1 } ||
                            extension.WeaponCooldownSeconds is { } cooldown &&
                            (!double.IsFinite(cooldown) || cooldown < 0 || cooldown > 3600) ||
                            extension.ThrustCapacityFraction is { } thrustCapacity &&
                            (!float.IsFinite(thrustCapacity) || thrustCapacity is < 0 or > 1))
                            throw new InvalidDataException("NPC equipment transfer extension is unsupported.");
                    }
                }
                foreach (var cargo in state.Cargo)
                {
                    if (GameData.Items.Equipment.Get(cargo.ItemId) is null)
                        throw new InvalidDataException($"Target is missing NPC cargo item '{cargo.ItemId}'.");
                }
                if (!string.IsNullOrWhiteSpace(state.FactionId) && GameData.Items.Factions.Get(state.FactionId) is null)
                    throw new InvalidDataException($"Target is missing NPC faction '{state.FactionId}'.");
                if (!string.IsNullOrWhiteSpace(state.PilotId) && GameData.Items.GetPilot(state.PilotId) is null)
                    throw new InvalidDataException($"Target is missing NPC pilot '{state.PilotId}'.");
                if (string.IsNullOrWhiteSpace(state.StateGraphId) ||
                    !GameData.Items.Ini.StateGraphDb.Tables.ContainsKey(
                        new StateGraphDescription(state.StateGraphId.ToUpperInvariant(), "LEADER")))
                    throw new InvalidDataException($"Target is missing NPC state graph '{state.StateGraphId}'.");
                if (!string.IsNullOrWhiteSpace(state.CommHeadId) && GameData.Items.Bodyparts.Get(state.CommHeadId) is null ||
                    !string.IsNullOrWhiteSpace(state.CommBodyId) && GameData.Items.Bodyparts.Get(state.CommBodyId) is null ||
                    !string.IsNullOrWhiteSpace(state.CommAccessoryId) && GameData.Items.Accessories.Get(state.CommAccessoryId) is null)
                    throw new InvalidDataException($"Target is missing NPC communications costume data for '{state.Nickname}'.");

                if (state.Autopilot.ExtensionData.Length == 0)
                    throw new InvalidDataException($"NPC '{state.Nickname}' has no transferable autopilot state.");
                var autopilot = JsonSerializer.Deserialize<LibreLancer.World.Components.AutopilotTransferState>(
                    state.Autopilot.ExtensionData)
                    ?? throw new InvalidDataException("NPC autopilot transfer state is empty.");
                autopilot.Validate();
                if (!string.Equals(state.Autopilot.Behavior, autopilot.Behavior.ToString(), StringComparison.Ordinal))
                    throw new InvalidDataException("NPC autopilot state does not match its declared behavior.");
                if (state.Autopilot.TargetNpcId != autopilot.TargetNpcId)
                    throw new InvalidDataException("NPC autopilot target identity does not match its stable target reference.");
                if (autopilot.Behavior == LibreLancer.World.Components.AutopilotBehaviors.Formation &&
                    !snapshot.Formations.Any(formation => formation.Members.Any(member =>
                        member.NpcId == identity.NpcId && !member.IsLeader)))
                    throw new InvalidDataException($"NPC '{state.Nickname}' has formation autopilot state without a follower slot.");

                if (state.Ai.StateId is not ("none" or "AiAttackState" or "AiDockState") ||
                    state.Ai.ExtensionData.Length == 0)
                    throw new InvalidDataException($"NPC '{state.Nickname}' has an unsupported AI state.");
                var ai = JsonSerializer.Deserialize<NpcAiTransferExtensionV1>(state.Ai.ExtensionData);
                if (ai is not { SchemaVersion: 3 })
                    throw new InvalidDataException("NPC AI transfer extension is unsupported.");
                LibreLancer.Server.Components.SNPCComponent.ValidateTransferState(ai);
                if (state.Ai.StateId == "AiDockState" &&
                    (autopilot.Behavior != LibreLancer.World.Components.AutopilotBehaviors.Dock ||
                     autopilot.GetTargetReference() != ai.DirectiveTarget))
                    throw new InvalidDataException($"NPC '{state.Nickname}' has inconsistent dock target state.");
                if (state.Ai.StateId is "AiAttackState" or "AiDockState")
                {
                    if (ai.DirectiveTarget is null ||
                        state.Ai.StateId == "AiDockState" &&
                        (ai.GotoKind is null || !Enum.IsDefined((LibreLancer.World.GotoKind)ai.GotoKind.Value)))
                        throw new InvalidDataException($"NPC '{state.Nickname}' has an incomplete directive state.");
                    if (state.Ai.StateId == "AiAttackState" && ai.DirectiveTarget.Kind == "npc" &&
                        ai.DirectiveTarget.NpcId != state.CurrentTargetNpcId)
                        throw new InvalidDataException($"NPC '{state.Nickname}' has inconsistent attack target identities.");
                }
                if (snapshot.MissionRuntimeId.HasValue)
                {
                    if (state.MissionState.Length == 0)
                        throw new InvalidDataException($"Mission NPC '{state.Nickname}' has no mission association.");
                    var context = JsonSerializer.Deserialize<NpcMissionTransferContext>(state.MissionState);
                    if (context is not { CharacterId: > 0 } ||
                        !string.Equals(context.MissionNickname, missionState!.MissionNickname,
                            StringComparison.OrdinalIgnoreCase) ||
                        missionCharacterId.HasValue && context.CharacterId != missionCharacterId.Value)
                        throw new InvalidDataException($"Mission NPC '{state.Nickname}' does not match the transferred mission runtime.");
                    missionCharacterId = context.CharacterId;
                }
            }
            if (snapshot.Formations.Any(formation => formation.Members.Any(member => member.CharacterId.HasValue) &&
                    (missionCharacterId is null || formation.Members.Any(member =>
                        member.CharacterId is { } characterId && characterId != missionCharacterId.Value))))
                throw new InvalidDataException("NPC formation player identity does not match the transferred mission runtime.");
        }

        /// <summary>Reserves, freezes and sends mission jumpers while leaving lease commit to the character transfer.</summary>
        public async Task PrepareAndSendNpcTransferAsync(Guid transferId, string targetInstanceId,
            string targetEndpoint, string targetSystemId, string target, long characterId, long leaseVersion,
            JumperNpc[] jumpers,
            ServerWorld? sourceWorld, MissionRuntime? missionRuntime, Action? drainMissionActions = null,
            CancellationToken cancellationToken = default)
        {
            if (jumpers.Length == 0)
                return;
            if (sourceWorld is null)
                throw new InvalidOperationException("NPC source world is unavailable for a cross-instance jump.");
            if (transferId == Guid.Empty || string.IsNullOrWhiteSpace(InstanceId) ||
                string.IsNullOrWhiteSpace(npcCoordinatorApiKey) || string.IsNullOrWhiteSpace(NpcCoordinatorUrl) ||
                NpcTransferPort is < 1 or > 65535 || NpcTransferCertificate is null || NpcTransferCaCertificate is null)
                throw new InvalidOperationException("NPC peer transfer is not configured on this instance.");

            var sourcePath = GetNpcSourceSnapshotPath(transferId);
            var npcIds = jumpers.Select(jumper => jumper.NpcId).ToArray();
            if (npcIds.Any(id => id == Guid.Empty) || npcIds.Distinct().Count() != npcIds.Length ||
                jumpers.Any(jumper => jumper.OwnershipVersion <= 0))
                throw new InvalidDataException("A jumping NPC has no Coordinator identity or ownership version.");
            var initialSnapshot = BuildNpcTransferSnapshot(transferId, targetSystemId, missionRuntime, jumpers, target);
            await WriteNpcSnapshotAtomicallyAsync(sourcePath, MessagePackSerializer.Serialize(initialSnapshot),
                cancellationToken).ConfigureAwait(false);

            var coordinator = new Uri(NpcCoordinatorUrl);
            using var prepareRequest = new HttpRequestMessage(HttpMethod.Post,
                new Uri(coordinator, "/internal/v1/npc-transfers/prepare"))
            {
                Content = JsonContent.Create(new NpcTransferPrepareRequest
                {
                    TransferId = transferId,
                    SourceInstanceId = InstanceId,
                    TargetInstanceId = targetInstanceId,
                    TargetSystemId = targetSystemId,
                    NpcIds = npcIds,
                    FormationId = initialSnapshot.Formations.FirstOrDefault()?.FormationId,
                    MissionRuntimeId = missionRuntime is null ? null : transferId,
                    ExpiresUtc = DateTime.UtcNow.AddSeconds(90),
                    IdempotencyKey = $"{transferId:N}:{(missionRuntime is null ? "population" : "mission-npcs")}"
                })
            };
            prepareRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
            using var prepareResponse = await npcTransferHttp.SendAsync(prepareRequest, cancellationToken).ConfigureAwait(false);
            if (!prepareResponse.IsSuccessStatusCode)
                throw new HttpRequestException($"Coordinator rejected NPC transfer reservation with HTTP {(int)prepareResponse.StatusCode}.");
            var prepared = await prepareResponse.Content.ReadFromJsonAsync<NpcTransferPrepared>(cancellationToken)
                .ConfigureAwait(false);
            if (prepared is not { Accepted: true } || prepared.TransferId != transferId ||
                string.IsNullOrWhiteSpace(prepared.TargetEndpoint))
                throw new InvalidDataException("Coordinator returned an invalid NPC transfer reservation.");

            if (missionRuntime is not null && (leaseVersion < 0 || missionRuntime.Player.Character?.ID != characterId))
                throw new InvalidDataException("Mission NPC transfer does not match its source character lease.");
            if (missionRuntime is not null && !transferLeaseVersions.TryAdd(transferId, leaseVersion) &&
                (!transferLeaseVersions.TryGetValue(transferId, out var knownVersion) || knownVersion != leaseVersion))
                throw new InvalidOperationException("Transfer ID is already associated with a different source lease.");

            NpcTransferSnapshot? frozenSnapshot = null;
            byte[]? frozenSnapshotBytes = null;
            if (missionRuntime is null)
            {
                await sourceWorld.FreezeJumpersAsync(jumpers, null, triggerMissionExit: false,
                    beforeRemove: captured =>
                    {
                        var capturedSnapshot = BuildNpcTransferSnapshot(transferId, targetSystemId, null, captured, target);
                        frozenSnapshot = capturedSnapshot;
                        var serialized = MessagePackSerializer.Serialize(capturedSnapshot);
                        frozenSnapshotBytes = serialized;
                        WriteNpcSnapshotAtomically(sourcePath, serialized, replaceExisting: true);
                    }).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var sourcePlayer = missionRuntime.Player;
                await sourcePlayer.FreezeForTransferAsync(transferId, characterId, targetSystemId, target,
                        async () =>
                        {
                            var frozen = await sourceWorld.FreezePlayerAndJumpersAsync(sourcePlayer, jumpers,
                                beforeFreeze: drainMissionActions,
                                beforeRemove: captured =>
                                {
                                    var capturedSnapshot = BuildNpcTransferSnapshot(transferId, targetSystemId,
                                        missionRuntime, captured, target);
                                    frozenSnapshot = capturedSnapshot;
                                    var serialized = MessagePackSerializer.Serialize(capturedSnapshot);
                                    frozenSnapshotBytes = serialized;
                                    WriteNpcSnapshotAtomically(sourcePath, serialized, replaceExisting: true);
                                }).ConfigureAwait(false);
                            return frozen.PlayerTransform;
                        })
                    .WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            var snapshot = frozenSnapshot
                ?? throw new InvalidDataException("Source NPC freeze completed without a durable runtime snapshot.");
            var snapshotBytes = frozenSnapshotBytes
                ?? throw new InvalidDataException("Source NPC freeze completed without serialized runtime state.");

            await AdvanceNpcTransferAsync(transferId, NpcTransferState.SourceFrozen, snapshot, cancellationToken)
                .ConfigureAwait(false);
            var endpoint = await ResolveNpcPeerEndpointAsync(prepared.TargetEndpoint, NpcTransferPort, cancellationToken, prepared.NpcTransferEndpoint)
                .ConfigureAwait(false);
            var peerRequest = new NpcPeerSnapshotTransfer
            {
                TransferId = transferId,
                SourceInstanceId = InstanceId,
                TargetInstanceId = targetInstanceId,
                SnapshotMessagePack = snapshotBytes
            };
            var acknowledgement = await NpcTransferQuicReceiver.SendAsync(endpoint, targetInstanceId,
                NpcTransferCertificate, NpcTransferCaCertificate, peerRequest, cancellationToken).ConfigureAwait(false);
            if (!acknowledgement.Accepted)
                throw new InvalidOperationException($"Target rejected NPC snapshot: {acknowledgement.ReasonCode}.");
            if (missionRuntime is null)
            {
                await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task AbortNpcTransferAndRestoreAsync(Guid transferId, ServerWorld? sourceWorld,
            MissionRuntime? missionRuntime, CancellationToken cancellationToken = default)
        {
            while (!npcSourceTransferResolutions.TryAdd(transferId, 0))
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            try
            {
                var path = GetNpcSourceSnapshotPath(transferId);
                var recovery = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
                if (recovery is null)
                    return;
                if (recovery is { State: NpcTransferState.Committed or NpcTransferState.SourceReleased })
                    throw new InvalidOperationException("Committed NPC ownership cannot be rolled back.");
                if (!File.Exists(path) && recovery.State is NpcTransferState.Requested or NpcTransferState.Reserved)
                {
                    await AdvanceNpcTransferAsync(transferId, NpcTransferState.Aborted, null, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
                var payload = await ReadNpcSourceSnapshotPayloadAsync(transferId, recovery, path, cancellationToken)
                    .ConfigureAwait(false);
                if (payload.Length == 0)
                    return;
                var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(payload,
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                NpcTransferContractValidator.Validate(snapshot);
                if (snapshot.MissionRuntimeId.HasValue && missionRuntime is null)
                    missionRuntime = FindNpcMissionRuntime(snapshot);
                if (snapshot.MissionRuntimeId.HasValue && missionRuntime is null)
                    return;
                try
                {
                    if (recovery is not null && recovery.State != NpcTransferState.Aborted)
                        await AdvanceNpcTransferAsync(transferId, NpcTransferState.Aborted, null, cancellationToken)
                            .ConfigureAwait(false);
                }
                catch (HttpRequestException exception)
                {
                    FLLog.Warning("NPC Transfer", $"Coordinator abort will be retried for {transferId:D}: {exception.Message}");
                    throw;
                }
                if (sourceWorld is null)
                    sourceWorld = await RequestNpcWorldAsync(snapshot.Npcs[0].SystemId, cancellationToken).ConfigureAwait(false);
                var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                sourceWorld.EnqueueAction(() =>
                {
                    try
                    {
                        sourceWorld.NPCs.RestoreTransfer(snapshot, missionRuntime, incrementOwnershipVersion: false,
                            expectedSystemId: snapshot.Npcs[0].SystemId, emitMissionSystemTransition: false);
                        if (!snapshot.MissionRuntimeId.HasValue)
                        {
                            var failedGate = sourceWorld.GameWorld.Objects.FirstOrDefault(obj =>
                                obj.TryGetComponent<Components.SDockableComponent>(out var dockable) &&
                                dockable.Action.Kind == LibreLancer.Data.GameData.World.DockKinds.Jump &&
                                string.Equals(dockable.Action.Target, snapshot.TargetSystemId, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(dockable.Action.Exit, snapshot.TargetArrivalObject, StringComparison.OrdinalIgnoreCase));
                            sourceWorld.Population.ResumeFailedTraderGroup(snapshot.NpcIds, failedGate?.Nickname ?? "");
                        }
                        File.Delete(path);
                        restored.TrySetResult();
                    }
                    catch (Exception exception) { restored.TrySetException(exception); }
                });
                await restored.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { npcSourceTransferResolutions.TryRemove(transferId, out _); }
        }

        private async Task CommitNpcTransferAsync(Guid transferId, CancellationToken cancellationToken)
        {
            while (!npcSourceTransferResolutions.TryAdd(transferId, 0))
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            try
            {
            var result = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (result is null)
                throw new InvalidDataException("Coordinator lost the prepared NPC transfer journal.");
            if (result.State == NpcTransferState.TargetAccepted)
                await AdvanceNpcTransferAsync(transferId, NpcTransferState.Committed, null, cancellationToken).ConfigureAwait(false);
            else if (result.State is not (NpcTransferState.Committed or NpcTransferState.SourceReleased))
                throw new InvalidOperationException($"NPC transfer cannot commit from state {result.State}.");
            }
            finally { npcSourceTransferResolutions.TryRemove(transferId, out _); }
        }

        private async Task MarkNpcTransferSourceReleasedAsync(Guid transferId, CancellationToken cancellationToken)
        {
            var result = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (result is null)
                throw new InvalidDataException("Coordinator lost the committed NPC transfer journal.");
            if (result.State == NpcTransferState.Committed)
                await AdvanceNpcTransferAsync(transferId, NpcTransferState.SourceReleased, null, cancellationToken)
                    .ConfigureAwait(false);
            else if (result.State != NpcTransferState.SourceReleased)
                throw new InvalidOperationException($"NPC source cannot be released from state {result.State}.");

            var path = GetNpcSourceSnapshotPath(transferId);
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>Recovers source-side NPC handoffs even when the player object was lost during a process restart.</summary>
        public async Task ResolvePendingSourceNpcTransfersAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(TransferInstanceKey) || string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) ||
                string.IsNullOrWhiteSpace(NpcCoordinatorUrl) || string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
                return;
            Directory.CreateDirectory(NpcTransferStagingDirectory);
            var transferIds = new HashSet<Guid>();
            var populationTransferIds = new HashSet<Guid>();
            foreach (var path in Directory.EnumerateFiles(NpcTransferStagingDirectory, "*.source.msgpack"))
            {
                var idText = Path.GetFileName(path)[..^".source.msgpack".Length];
                if (Guid.TryParseExact(idText, "N", out var transferId))
                {
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(bytes,
                        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                    if (snapshot.MissionRuntimeId is null)
                        populationTransferIds.Add(transferId);
                    else
                        transferIds.Add(transferId);
                }
            }

            await ResolvePendingPopulationNpcTransfersAsync(populationTransferIds, cancellationToken).ConfigureAwait(false);

            try
            {
                Guid? cursor = null;
                do
                {
                    var query = $"/internal/v1/npc-transfers/source-recovery?instanceId={Uri.EscapeDataString(InstanceId!)}&limit=128" +
                                (cursor is Guid after ? $"&afterTransferId={after:D}" : "");
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        new Uri(new Uri(NpcCoordinatorUrl), query));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
                    using var response = await npcTransferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var page = await response.Content.ReadFromJsonAsync<NpcTransferRecoveryPageV1>(cancellationToken)
                        .ConfigureAwait(false) ?? throw new InvalidDataException("Coordinator source recovery page is empty.");
                    if (page.TransferIds.Any(id => id == Guid.Empty) ||
                        page.TransferIds.Zip(page.TransferIds.Skip(1), (left, right) => IsNpcTransferCursorAfter(left, right))
                            .Any(isOutOfOrder => isOutOfOrder) ||
                        (cursor is Guid previous && page.TransferIds.Any(id => !IsNpcTransferCursorAfter(id, previous))) ||
                        (page.NextAfterTransferId is Guid next &&
                         ((cursor is Guid cursorValue && !IsNpcTransferCursorAfter(next, cursorValue)) ||
                          page.TransferIds.Any(id => IsNpcTransferCursorAfter(id, next)))) )
                        throw new InvalidDataException("Coordinator source recovery page cursor is invalid.");
                    transferIds.UnionWith(page.TransferIds);
                    cursor = page.NextAfterTransferId;
                } while (cursor is not null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                FLLog.Warning("NPC Transfer", $"Could not enumerate pending source NPC transfers: {exception.Message}");
            }

            foreach (var transferId in transferIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var status = await GetTransferStatusAsync(transferId, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(status.SourceInstanceId, InstanceId, StringComparison.Ordinal))
                        continue;
                    switch (status.State)
                    {
                        case LancerNexus.Protocol.TransferState.Committed:
                            await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                            var sourcePlayer = GetConnectedPlayers().FirstOrDefault(candidate => candidate.GatewayTransferId == transferId);
                            if (sourcePlayer?.TransferInProgress == true)
                            {
                                if (!sourcePlayer.ReleaseAfterTransferCommit(transferId))
                                    throw new InvalidOperationException("Source character is not frozen for this transfer.");
                            }
                            else if (sourcePlayer?.Character != null)
                            {
                                throw new InvalidOperationException("Source character is still active after the target lease committed.");
                            }
                            await NotifyTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                            await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                            break;
                        case LancerNexus.Protocol.TransferState.SourceReleased:
                            await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                            var releasedPlayer = GetConnectedPlayers().FirstOrDefault(candidate => candidate.GatewayTransferId == transferId);
                            if (releasedPlayer?.TransferInProgress == true)
                            {
                                if (!releasedPlayer.ReleaseAfterTransferCommit(transferId))
                                    throw new InvalidOperationException("Source character is not frozen for this transfer.");
                            }
                            else if (releasedPlayer?.Character != null)
                            {
                                throw new InvalidOperationException("Source character is still active after the target lease committed.");
                            }
                            await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                            break;
                        case LancerNexus.Protocol.TransferState.Aborted:
                        case LancerNexus.Protocol.TransferState.Rejected:
                        case LancerNexus.Protocol.TransferState.Expired:
                        case LancerNexus.Protocol.TransferState.TimedOut:
                            var player = GetConnectedPlayers().FirstOrDefault(candidate => candidate.GatewayTransferId == transferId);
                            await AbortNpcTransferAndRestoreAsync(transferId, player?.Space?.World,
                                player?.MissionRuntime, cancellationToken).ConfigureAwait(false);
                            break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    FLLog.Warning("NPC Transfer", $"Could not recover source NPC transfer {transferId:D}: {exception}");
                }
            }
        }

        private async Task ResolvePendingPopulationNpcTransfersAsync(IEnumerable<Guid> transferIds,
            CancellationToken cancellationToken)
        {
            foreach (var transferId in transferIds)
            {
                try
                {
                    var path = GetNpcSourceSnapshotPath(transferId);
                    var recovery = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken,
                        includeSnapshot: true).ConfigureAwait(false);
                    if (recovery is null || recovery.State is NpcTransferState.Reserved or NpcTransferState.Aborted)
                    {
                        await AbortNpcTransferAndRestoreAsync(transferId, null, null, cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }
                    if (recovery.SourceInstanceId != InstanceId || recovery.TargetInstanceId.Length == 0)
                        throw new InvalidDataException("Autonomous NPC recovery journal belongs to another instance.");

                    if (recovery.State == NpcTransferState.TargetAccepted)
                    {
                        await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                        await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (recovery.State is NpcTransferState.Committed or NpcTransferState.SourceReleased)
                    {
                        await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    if (recovery.State != NpcTransferState.SourceFrozen)
                        continue;

                    var payload = await ReadNpcSourceSnapshotPayloadAsync(transferId, recovery, path, cancellationToken)
                        .ConfigureAwait(false);
                    var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(payload,
                        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                    using var targetRequest = new HttpRequestMessage(HttpMethod.Post,
                        new Uri(new Uri(NpcCoordinatorUrl!), "/internal/v1/npc-transfers/target"))
                    {
                        Content = JsonContent.Create(new NpcTransferTargetResolveRequestV1
                        {
                            SourceInstanceId = InstanceId!,
                            TargetSystemId = recovery.TargetSystemId
                        })
                    };
                    targetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
                    using var targetResponse = await npcTransferHttp.SendAsync(targetRequest, cancellationToken)
                        .ConfigureAwait(false);
                    targetResponse.EnsureSuccessStatusCode();
                    var target = await targetResponse.Content.ReadFromJsonAsync<NpcTransferTargetResolveResultV1>(cancellationToken)
                        .ConfigureAwait(false);
                    if (target is not { Found: true } || target.TargetInstanceId != recovery.TargetInstanceId ||
                        string.IsNullOrWhiteSpace(target.TargetEndpoint))
                        continue;
                    var endpoint = await ResolveNpcPeerEndpointAsync(target.TargetEndpoint, NpcTransferPort, cancellationToken, target.NpcTransferEndpoint)
                        .ConfigureAwait(false);
                    var acknowledgement = await NpcTransferQuicReceiver.SendAsync(endpoint, recovery.TargetInstanceId,
                        NpcTransferCertificate!, NpcTransferCaCertificate!, new NpcPeerSnapshotTransfer
                        {
                            TransferId = transferId,
                            SourceInstanceId = InstanceId!,
                            TargetInstanceId = recovery.TargetInstanceId,
                            SnapshotMessagePack = payload
                        }, cancellationToken).ConfigureAwait(false);
                    if (acknowledgement.Accepted)
                    {
                        await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                        await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    FLLog.Warning("NPC Transfer", $"Autonomous transfer recovery {transferId:D} remains pending: {exception}");
                }
            }
        }

        private async Task<byte[]> ReadNpcSourceSnapshotPayloadAsync(Guid transferId,
            NpcTransferRecoveryWireRecord? recovery, string path, CancellationToken cancellationToken)
        {
            byte[] payload = File.Exists(path)
                ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                : [];
            if (recovery is null)
            {
                if (payload.Length == 0)
                    throw new InvalidDataException("NPC source snapshot is missing locally and from Coordinator recovery.");
                return payload;
            }
            if (recovery.TransferId != transferId || recovery.SourceInstanceId != InstanceId)
                throw new InvalidDataException("NPC source recovery record belongs to another transfer or instance.");
            if (string.IsNullOrWhiteSpace(recovery.SnapshotSha256))
            {
                if (payload.Length == 0)
                    throw new InvalidDataException("NPC source snapshot has not reached the Coordinator journal.");
                return payload;
            }

            byte[] expectedHash;
            try { expectedHash = Convert.FromHexString(recovery.SnapshotSha256); }
            catch (FormatException exception)
            {
                throw new InvalidDataException("NPC source journal snapshot hash is malformed.", exception);
            }
            if (expectedHash.Length != SHA256.HashSizeInBytes)
                throw new InvalidDataException("NPC source journal snapshot hash has an invalid size.");
            if (CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(payload)))
                return payload;

            var authoritative = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken, includeSnapshot: true)
                .ConfigureAwait(false);
            if (authoritative?.Snapshot is not { } snapshot || authoritative.TransferId != transferId ||
                authoritative.SourceInstanceId != InstanceId ||
                authoritative.State is not (NpcTransferState.SourceFrozen or NpcTransferState.TargetAccepted or NpcTransferState.Aborted) ||
                !string.Equals(authoritative.SnapshotSha256, recovery.SnapshotSha256, StringComparison.OrdinalIgnoreCase) ||
                !authoritative.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("Coordinator source recovery snapshot is missing or inconsistent.");
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.TransferId != transferId || !snapshot.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("Coordinator source recovery snapshot does not match its journal record.");
            payload = MessagePackSerializer.Serialize(snapshot);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(payload)))
                throw new InvalidDataException("Coordinator source recovery snapshot does not match its journal hash.");
            await WriteNpcSnapshotAtomicallyAsync(path, payload, cancellationToken, replaceExisting: true)
                .ConfigureAwait(false);
            return payload;
        }

        private async Task<NpcTransferRecoveryWireRecord?> ReadNpcTransferRecoveryAsync(Guid transferId,
            CancellationToken cancellationToken, bool includeSnapshot = false)
        {
            var url = new Uri(new Uri(NpcCoordinatorUrl!),
                $"/internal/v1/npc-transfers/{transferId:D}/recovery?instanceId={Uri.EscapeDataString(InstanceId!)}" +
                (includeSnapshot ? "&includeSnapshot=true" : ""));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
            using var response = await npcTransferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NpcTransferRecoveryWireRecord>(cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task AdvanceNpcTransferAsync(Guid transferId, NpcTransferState state,
            NpcTransferSnapshot? snapshot, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(NpcCoordinatorUrl!), $"/internal/v1/npc-transfers/{transferId:D}/phase"))
            {
                Content = JsonContent.Create(new NpcTransferPhaseRequest
                {
                    TransferId = transferId,
                    State = state,
                    Snapshot = snapshot
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
            using var response = await npcTransferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<NpcTransferPhaseWireResult>(cancellationToken)
                .ConfigureAwait(false);
            if (result is not { Accepted: true } || result.TransferId != transferId || result.State != state)
                throw new InvalidDataException($"Coordinator rejected NPC transfer phase {state}.");
        }

        private string GetNpcSourceSnapshotPath(Guid transferId) =>
            Path.Combine(NpcTransferStagingDirectory!, $"{transferId:N}.source.msgpack");

        private static NpcTransferSnapshot BuildNpcTransferSnapshot(Guid transferId, string targetSystemId,
            MissionRuntime? missionRuntime, JumperNpc[] jumpers, string? targetArrivalObject = null)
        {
            var npcIds = jumpers.Select(jumper => jumper.NpcId).ToArray();
            if (npcIds.Any(id => id == Guid.Empty) || npcIds.Distinct().Count() != npcIds.Length ||
                jumpers.Any(jumper => jumper.RuntimeSnapshot is null || jumper.OwnershipVersion <= 0))
                throw new InvalidDataException("A jumping NPC has no Coordinator identity or transferable runtime snapshot.");
            var characterId = missionRuntime?.Player.Character?.ID;
            var formations = CaptureNpcFormations(transferId, characterId, jumpers);
            foreach (var jumper in jumpers)
            {
                var runtime = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(jumper.RuntimeSnapshot.RuntimeState);
                if (runtime.Ai.StateId is not ("none" or "AiAttackState" or "AiDockState") ||
                    runtime.Autopilot.Behavior is not ("None" or "Goto" or "Dock" or "Formation" or "Undock") ||
                    runtime.Autopilot.ExtensionData.Length == 0 ||
                    runtime.Ai.ExtensionData.Length == 0 ||
                    missionRuntime is not null && runtime.MissionState.Length == 0)
                    throw new InvalidDataException($"NPC '{runtime.Nickname}' has runtime state that this GameServer cannot restore safely.");
                var autopilot = JsonSerializer.Deserialize<LibreLancer.World.Components.AutopilotTransferState>(
                    runtime.Autopilot.ExtensionData)
                    ?? throw new InvalidDataException($"NPC '{runtime.Nickname}' has an empty autopilot state.");
                autopilot.Validate();
                if (!string.Equals(runtime.Autopilot.Behavior, autopilot.Behavior.ToString(), StringComparison.Ordinal) ||
                    runtime.Autopilot.TargetNpcId != autopilot.TargetNpcId ||
                    autopilot.Behavior == LibreLancer.World.Components.AutopilotBehaviors.Formation &&
                    !formations.Any(formation => formation.Members.Any(member =>
                        member.NpcId == jumper.NpcId && !member.IsLeader)))
                    throw new InvalidDataException($"NPC '{runtime.Nickname}' has inconsistent autopilot transfer state.");
                var aiExtension = JsonSerializer.Deserialize<NpcAiTransferExtensionV1>(runtime.Ai.ExtensionData);
                if (aiExtension is not { SchemaVersion: 3 } ||
                    (runtime.Ai.StateId is "AiAttackState" or "AiDockState") && aiExtension.DirectiveTarget is null)
                    throw new InvalidDataException($"NPC '{runtime.Nickname}' has unsupported or incomplete AI runtime state.");
                if (missionRuntime is not null)
                {
                    ValidateMissionNpcContext(missionRuntime, runtime);
                }
            }
            var snapshot = new NpcTransferSnapshot
            {
                TransferId = transferId,
                NpcIds = npcIds,
                Formations = formations,
                MissionRuntimeId = missionRuntime is null ? null : transferId,
                Npcs = missionRuntime is null
                    ? jumpers.Select(PreparePopulationRuntimeSnapshot).ToArray()
                    : jumpers.Select(jumper => jumper.RuntimeSnapshot).ToArray(),
                TargetSystemId = targetSystemId,
                TargetArrivalObject = targetArrivalObject,
                MissionRuntimeState = missionRuntime is null
                    ? []
                    : MessagePackSerializer.Serialize(missionRuntime.CaptureTransferState())
            };
            try
            {
                NpcTransferContractValidator.Validate(snapshot);
            }
            catch (LancerNexus.Protocol.ProtocolViolationException exception) when (snapshot.Formations.Length > 0)
            {
                var formationDetails = string.Join("; ", snapshot.Formations.Select(formation =>
                    $"id={formation.FormationId:D}, members={formation.Members.Length}, " +
                    $"leaders={formation.Members.Count(member => member.IsLeader)}, " +
                    $"playerPosition={FormatVector(formation.PlayerPosition)}, " +
                    $"playerTarget={FormatVector(formation.PlayerTargetPosition)}"));
                throw new LancerNexus.Protocol.ProtocolViolationException(
                    $"{exception.Message} Formation details: {formationDetails}");
            }
            return snapshot;
        }

        internal static void ValidateMissionNpcContext(MissionRuntime missionRuntime, NpcRuntimeStateV1 state)
        {
            var context = state.MissionState.Length == 0 ? null :
                JsonSerializer.Deserialize<NpcMissionTransferContext>(state.MissionState);
            if (missionRuntime.Player.Character?.ID is not > 0 || context is not { CharacterId: > 0 } ||
                context.CharacterId != missionRuntime.Player.Character.ID ||
                string.IsNullOrWhiteSpace(context.MissionNickname) ||
                !string.Equals(missionRuntime.MissionNickname, context.MissionNickname,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"NPC '{state.Nickname}' belongs to a different mission owner or runtime.");
        }

        private static string FormatVector(NpcVector3? value) => value is null
            ? "null"
            : $"({value.X:R},{value.Y:R},{value.Z:R})";

        private static NpcRuntimeSnapshot PreparePopulationRuntimeSnapshot(JumperNpc jumper)
        {
            var runtime = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(jumper.RuntimeSnapshot.RuntimeState);
            var autopilot = JsonSerializer.Deserialize<LibreLancer.World.Components.AutopilotTransferState>(
                runtime.Autopilot.ExtensionData)
                ?? throw new InvalidDataException($"NPC '{runtime.Nickname}' has an empty autopilot state.");
            var resumedAutopilot = autopilot.Behavior == LibreLancer.World.Components.AutopilotBehaviors.Formation
                ? autopilot
                : new LibreLancer.World.Components.AutopilotTransferState
                {
                    Behavior = LibreLancer.World.Components.AutopilotBehaviors.None,
                    PitchController = autopilot.PitchController,
                    YawController = autopilot.YawController
                };
            var ai = JsonSerializer.Deserialize<NpcAiTransferExtensionV1>(runtime.Ai.ExtensionData)
                ?? throw new InvalidDataException($"NPC '{runtime.Nickname}' has an empty AI state.");
            var resumedAi = ai with
            {
                DirectiveTarget = null,
                SelectedTarget = null,
                GotoKind = null,
                StayInRangeTarget = null,
                StayInRangePoint = new NpcTransferVectorV1(0, 0, 0),
                StayInRangeRadius = 0
            };
            var copy = new NpcRuntimeStateV1
            {
                Position = runtime.Position,
                Orientation = runtime.Orientation,
                LinearVelocity = runtime.LinearVelocity,
                AngularVelocity = runtime.AngularVelocity,
                Health = runtime.Health,
                LoadoutArchetype = runtime.LoadoutArchetype,
                Equipment = runtime.Equipment,
                Cargo = runtime.Cargo,
                Autopilot = new NpcAutopilotState
                {
                    Behavior = resumedAutopilot.Behavior.ToString(),
                    TargetPosition = resumedAutopilot.Behavior == LibreLancer.World.Components.AutopilotBehaviors.Goto
                        ? new NpcVector3
                        {
                            X = resumedAutopilot.TargetPosition.X,
                            Y = resumedAutopilot.TargetPosition.Y,
                            Z = resumedAutopilot.TargetPosition.Z
                        }
                        : null,
                    Throttle = resumedAutopilot.MaxThrottle,
                    Cruise = resumedAutopilot.CanCruise,
                    BehaviorElapsedSeconds = runtime.Autopilot.BehaviorElapsedSeconds,
                    ExtensionData = JsonSerializer.SerializeToUtf8Bytes(resumedAutopilot)
                },
                Ai = new NpcAiState
                {
                    StateId = "none",
                    PreviousStateId = runtime.Ai.PreviousStateId,
                    StateElapsedSeconds = runtime.Ai.StateElapsedSeconds,
                    Timers = runtime.Ai.Timers,
                    RandomState = runtime.Ai.RandomState,
                    ExtensionData = JsonSerializer.SerializeToUtf8Bytes(resumedAi)
                },
                CurrentTargetNpcId = null,
                MissionState = [],
                Nickname = runtime.Nickname,
                DisplayName = runtime.DisplayName,
                FactionId = runtime.FactionId,
                PilotId = runtime.PilotId,
                StateGraphId = runtime.StateGraphId,
                CommHeadId = runtime.CommHeadId,
                CommBodyId = runtime.CommBodyId,
                CommAccessoryId = runtime.CommAccessoryId,
                StructuralParts = runtime.StructuralParts
            };
            return new NpcRuntimeSnapshot
            {
                NpcId = jumper.NpcId,
                OwnershipVersion = jumper.OwnershipVersion,
                SystemId = jumper.RuntimeSnapshot.SystemId,
                RuntimeSchemaVersion = jumper.RuntimeSnapshot.RuntimeSchemaVersion,
                RuntimeState = MessagePackSerializer.Serialize(copy)
            };
        }

        internal static NpcFormationStateV1[] CaptureNpcFormations(Guid transferId, long? characterId,
            JumperNpc[] jumpers)
        {
            var transferNpcIds = jumpers.Select(jumper => jumper.NpcId).ToHashSet();
            return jumpers.Where(jumper => jumper.FormationSnapshot is not null)
                .GroupBy(jumper => jumper.SourceFormationId)
                .Select(group =>
                {
                    var captured = group.First().FormationSnapshot!;
                    if (group.Any(jumper => jumper.FormationSnapshot is null ||
                            !SameFormationMembers(captured.Members, jumper.FormationSnapshot.Members)) ||
                        captured.Members.Any(member => member.NpcId is { } npcId && !transferNpcIds.Contains(npcId) ||
                            member.CharacterId is { } id && (characterId is null || id != characterId.Value) ||
                            !member.NpcId.HasValue && !member.CharacterId.HasValue))
                        throw new InvalidDataException("A mission NPC formation is incomplete or contains an unrelated member.");
                    var memberKey = string.Join("|", captured.Members.Select(member =>
                        member.NpcId is { } npcId ? $"npc:{npcId:N}" : $"character:{member.CharacterId}"));
                    var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{transferId:N}:{memberKey}"));
                    return new NpcFormationStateV1
                    {
                        FormationId = new Guid(digest.AsSpan(0, 16)),
                        Members = captured.Members,
                        PlayerPosition = captured.PlayerPosition,
                        PlayerTargetPosition = captured.PlayerTargetPosition
                    };
                }).ToArray();
        }

        private static bool SameFormationMembers(NpcFormationMemberV1[] left, NpcFormationMemberV1[] right) =>
            left.Length == right.Length && left.Zip(right).All(pair =>
                pair.First.IsLeader == pair.Second.IsLeader && pair.First.NpcId == pair.Second.NpcId &&
                pair.First.CharacterId == pair.Second.CharacterId && pair.First.Offset.X == pair.Second.Offset.X &&
                pair.First.Offset.Y == pair.Second.Offset.Y && pair.First.Offset.Z == pair.Second.Offset.Z);

        private static async Task WriteNpcSnapshotAtomicallyAsync(string path, byte[] payload,
            CancellationToken cancellationToken, bool replaceExisting = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && !replaceExisting)
            {
                var existing = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(payload)))
                    throw new InvalidDataException("A conflicting source NPC snapshot already exists for this transfer ID.");
                return;
            }
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    output.Flush(flushToDisk: true);
                }
                File.Move(tempPath, path, overwrite: replaceExisting);
            }
            finally { try { File.Delete(tempPath); } catch (IOException) { } }
        }

        private static void WriteNpcSnapshotAtomically(string path, byte[] payload, bool replaceExisting)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && !replaceExisting)
            {
                var existing = File.ReadAllBytes(path);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(payload)))
                    throw new InvalidDataException("A conflicting source NPC snapshot already exists for this transfer ID.");
                return;
            }
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           81920, FileOptions.WriteThrough))
                {
                    output.Write(payload);
                    output.Flush(flushToDisk: true);
                }
                try { File.Move(tempPath, path, overwrite: replaceExisting); }
                catch (IOException) when (File.Exists(path))
                {
                    var existing = File.ReadAllBytes(path);
                    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), SHA256.HashData(payload)))
                        throw new InvalidDataException("A conflicting source NPC snapshot already exists for this transfer ID.");
                }
            }
            finally { try { File.Delete(tempPath); } catch (IOException) { } }
        }

        internal static Uri ParseNpcPeerEndpoint(string targetEndpoint, int transferPort, string? privateEndpoint = null)
        {
            if (privateEndpoint is not null)
            {
                if (!NpcTransferContractValidator.IsValidPeerEndpoint(privateEndpoint))
                    throw new InvalidDataException("Coordinator NPC peer endpoint is invalid.");
                return new Uri(privateEndpoint, UriKind.Absolute);
            }
            if (transferPort is < 1 or > 65535)
                throw new InvalidDataException("Legacy NPC transfer port is invalid.");
            var endpointText = targetEndpoint.Contains("://", StringComparison.Ordinal)
                ? targetEndpoint
                : $"udp://{targetEndpoint}";
            if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("udp" or "quic" or "http" or "https") ||
                string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.AbsolutePath is not ("" or "/"))
                throw new InvalidDataException("Coordinator target endpoint is invalid.");
            // Old game endpoints (including quic:// game endpoints) never advertise a peer port.
            return new UriBuilder(uri) { Port = transferPort }.Uri;
        }

        private async Task<IPEndPoint> ResolveNpcPeerEndpointAsync(string targetEndpoint, int transferPort,
            CancellationToken cancellationToken, string? privateEndpoint = null)
        {
            var uri = ParseNpcPeerEndpoint(targetEndpoint, transferPort, privateEndpoint);
            if (IPAddress.TryParse(uri.Host, out var address))
                return new IPEndPoint(address, uri.Port);
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
            if (addresses.Length == 0)
                throw new InvalidDataException("Coordinator target host did not resolve.");
            return new IPEndPoint(addresses[0], uri.Port);
        }

        private async Task<ServerWorld> RequestNpcWorldAsync(string systemId, CancellationToken cancellationToken)
        {
            var system = GameData.Items.Systems.Get(systemId)
                ?? throw new InvalidDataException($"NPC system '{systemId}' is unknown.");
            var result = new TaskCompletionSource<ServerWorld>(TaskCreationOptions.RunContinuationsAsynchronously);
            Worlds.RequestWorld(system, result.SetResult, []);
            return await result.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private MissionRuntime? FindNpcMissionRuntime(NpcTransferSnapshot snapshot)
        {
            if (snapshot.Npcs.Length == 0)
                return null;
            var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(snapshot.Npcs[0].RuntimeState);
            var context = JsonSerializer.Deserialize<NpcMissionTransferContext>(state.MissionState);
            if (context is not { CharacterId: > 0 } || string.IsNullOrWhiteSpace(context.MissionNickname))
                return null;
            return GetConnectedPlayers().FirstOrDefault(player => player.Character?.ID == context.CharacterId &&
                player.MissionRuntime != null && string.Equals(player.MissionRuntime.MissionNickname,
                    context.MissionNickname, StringComparison.OrdinalIgnoreCase))?.MissionRuntime;
        }

        private sealed record NpcTransferPhaseWireResult(bool Accepted, string ReasonCode, Guid TransferId, NpcTransferState State);
        private sealed record NpcMissionTransferContext(long CharacterId, string MissionNickname);

        /// <summary>Restores locally staged NPC snapshots after the Coordinator commits their ownership leases.</summary>
        public async Task ResolvePendingNpcTransfersAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory) || string.IsNullOrWhiteSpace(NpcCoordinatorUrl) ||
                string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
                return;
            Directory.CreateDirectory(NpcTransferStagingDirectory);
            npcTransferHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
            await ResolveCommittedNpcTransferJournalsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var path in Directory.EnumerateFiles(NpcTransferStagingDirectory, "*.msgpack"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var transferId) ||
                    npcTransferCompleted.ContainsKey(transferId) ||
                    !npcTransferRestores.TryAdd(transferId, 0))
                    continue;
                try
                {
                    if (IsNpcTransferActivated(transferId))
                    {
                        npcTransferCompleted.TryAdd(transferId, 0);
                        continue;
                    }
                    await RestoreCommittedNpcTransferAsync(transferId, path, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    FLLog.Warning("NPC Transfer", $"Pending transfer {transferId:D} remains staged: {exception.Message}");
                }
                finally
                {
                    npcTransferRestores.TryRemove(transferId, out _);
                }
            }
        }

        private async Task ResolveCommittedNpcTransferJournalsAsync(CancellationToken cancellationToken)
        {
            Guid? cursor = null;
            do
            {
                var query = $"/internal/v1/npc-transfers/recovery?instanceId={Uri.EscapeDataString(InstanceId!)}&limit=128" +
                            (cursor is Guid after ? $"&afterTransferId={after:D}" : "");
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(NpcCoordinatorUrl!), query));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", npcCoordinatorApiKey);
                using var response = await npcTransferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var page = await response.Content.ReadFromJsonAsync<NpcTransferRecoveryPageV1>(cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidDataException("Coordinator NPC recovery page is empty.");
                if (page.TransferIds.Any(id => id == Guid.Empty) ||
                    page.TransferIds.Zip(page.TransferIds.Skip(1), (left, right) => IsNpcTransferCursorAfter(left, right))
                        .Any(isOutOfOrder => isOutOfOrder) ||
                    (cursor is Guid previous && page.TransferIds.Any(id => !IsNpcTransferCursorAfter(id, previous))) ||
                    (page.NextAfterTransferId is Guid next &&
                     ((cursor is Guid cursorValue && !IsNpcTransferCursorAfter(next, cursorValue)) ||
                      page.TransferIds.Any(id => IsNpcTransferCursorAfter(id, next)))))
                    throw new InvalidDataException("Coordinator NPC recovery page cursor is invalid.");

                foreach (var transferId in page.TransferIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (npcTransferCompleted.ContainsKey(transferId) ||
                        !npcTransferRestores.TryAdd(transferId, 0))
                        continue;
                    try
                    {
                        if (IsNpcTransferActivated(transferId))
                        {
                            npcTransferCompleted.TryAdd(transferId, 0);
                            continue;
                        }
                        var path = Path.Combine(NpcTransferStagingDirectory!, $"{transferId:N}.msgpack");
                        await RestoreCommittedNpcTransferAsync(transferId, path, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        FLLog.Warning("NPC Transfer", $"Journal transfer {transferId:D} remains pending: {exception.Message}");
                    }
                    finally
                    {
                        npcTransferRestores.TryRemove(transferId, out _);
                    }
                }

                cursor = page.NextAfterTransferId;
            } while (cursor is not null);
        }

        private static bool IsNpcTransferCursorAfter(Guid value, Guid cursor) =>
            string.CompareOrdinal(value.ToString("D"), cursor.ToString("D")) > 0;

        private async Task RestoreCommittedNpcTransferAsync(Guid transferId, string path, CancellationToken cancellationToken)
        {
            if (IsNpcTransferActivated(transferId))
                return;
            var url = new Uri(new Uri(NpcCoordinatorUrl!),
                $"/internal/v1/npc-transfers/{transferId:D}/recovery?instanceId={Uri.EscapeDataString(InstanceId!)}");
            using var response = await npcTransferHttp.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;
            var recovery = await response.Content.ReadFromJsonAsync<NpcTransferRecoveryWireRecord>(cancellationToken)
                .ConfigureAwait(false);
            if (recovery is null || recovery.TransferId != transferId || recovery.TargetInstanceId != InstanceId)
                return;
            if (recovery.State == NpcTransferState.Aborted)
            {
                File.Delete(path);
                FLLog.Info("NPC Transfer", $"Removed staged snapshot for aborted transfer {transferId:D}.");
                return;
            }
            if (recovery.State == NpcTransferState.TargetAccepted)
            {
                if (!await TryCommitTargetAcceptedMissionNpcTransferAsync(transferId, recovery, cancellationToken)
                        .ConfigureAwait(false))
                    return;
                recovery = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
                if (recovery is null || recovery.TargetInstanceId != InstanceId)
                    return;
            }
            if (recovery.State is not (NpcTransferState.Committed or NpcTransferState.SourceReleased) ||
                string.IsNullOrWhiteSpace(recovery.SnapshotSha256) || !OwnsSystem(recovery.TargetSystemId))
                return;
            var payload = await ReadCommittedNpcSnapshotPayloadAsync(transferId, recovery, path, cancellationToken)
                .ConfigureAwait(false);
            var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(payload,
                MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.TransferId != transferId ||
                !string.Equals(snapshot.TargetSystemId, recovery.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                !snapshot.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("Staged NPC snapshot identity does not match the transfer journal.");

            MissionRuntime? missionRuntime = null;
            if (snapshot.MissionRuntimeId is Guid missionTransferId)
            {
                missionRuntime = FindNpcMissionRuntime(snapshot) ?? GetConnectedPlayers().FirstOrDefault(player =>
                    player.GatewayTransferId == missionTransferId && player.MissionRuntime != null)?.MissionRuntime;
                if (missionRuntime is null)
                    return;
                var missionPlayer = GetConnectedPlayers().FirstOrDefault(player => player.MissionRuntime == missionRuntime);
                if (missionPlayer?.Space is null || !missionPlayer.Space.World.Players.ContainsKey(missionPlayer))
                    return;
            }
            var system = GameData.Items.Systems.Get(recovery.TargetSystemId)
                ?? throw new InvalidDataException($"NPC transfer target system '{recovery.TargetSystemId}' is unknown.");
            var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Worlds.RequestWorld(system, world => world.EnqueueAction(() =>
            {
                try
                {
                    if (IsNpcTransferActivated(transferId))
                    {
                        restored.TrySetResult();
                        return;
                    }
                    snapshot = RebaseNpcSnapshotAtArrival(snapshot, world);
                    world.NPCs.RestoreTransfer(snapshot, missionRuntime);
                    if (snapshot.MissionRuntimeId is null)
                    {
                        var npcIds = snapshot.NpcIds.ToHashSet();
                        var populationGroup = world.GameWorld.Objects.Where(obj =>
                                obj.TryGetComponent<LibreLancer.Server.Components.SNPCComponent>(out var npc) &&
                                npcIds.Contains(npc.NpcId))
                            .ToArray();
                        world.Population.AdoptTransferredTraderGroup(populationGroup, snapshot.TargetArrivalObject);
                    }
                    MarkNpcTransferActivated(transferId);
                    npcTransferCompleted.TryAdd(transferId, 0);
                    restored.TrySetResult();
                }
                catch (Exception exception)
                {
                    restored.TrySetException(exception);
                }
            }), []);
            await restored.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> TryCommitTargetAcceptedMissionNpcTransferAsync(Guid transferId,
            NpcTransferRecoveryWireRecord recovery, CancellationToken cancellationToken)
        {
            if (recovery.State != NpcTransferState.TargetAccepted)
                return false;
            var journal = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken, includeSnapshot: true)
                .ConfigureAwait(false);
            if (journal is not { State: NpcTransferState.TargetAccepted, Snapshot: { } snapshot } ||
                journal.TransferId != transferId || journal.TargetInstanceId != InstanceId ||
                !journal.NpcIds.Order().SequenceEqual(snapshot.NpcIds.Order()))
                throw new InvalidDataException("Target-accepted mission NPC transfer has no matching durable snapshot.");
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.MissionRuntimeId is null)
                return false;
            if (snapshot.MissionRuntimeId != transferId)
                throw new InvalidDataException("Target-accepted mission NPC transfer has a mismatched mission runtime ID.");
            var import = await Database.GetTransferSnapshotImportAsync(transferId).ConfigureAwait(false);
            if (import is null)
                return false;
            var status = await GetTransferStatusAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (status.State is not (LancerNexus.Protocol.TransferState.Committed or
                                     LancerNexus.Protocol.TransferState.SourceReleased))
                return false;
            if (!NpcMissionTransferCommitGate.IsCommittedByCharacterTransfer(transferId, InstanceId!, import,
                    status, snapshot))
                throw new InvalidDataException("Character lease commit does not match the target-accepted mission NPC transfer.");
            await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
            return true;
        }

        internal static NpcTransferSnapshot RebaseNpcSnapshotAtArrival(NpcTransferSnapshot snapshot, ServerWorld world)
        {
            if (string.IsNullOrWhiteSpace(snapshot.TargetArrivalObject))
                return snapshot;
            var arrival = world.GameWorld.GetObject(snapshot.TargetArrivalObject)
                ?? throw new InvalidDataException($"NPC transfer arrival object '{snapshot.TargetArrivalObject}' is missing.");
            return RebaseNpcSnapshotAtArrival(snapshot, arrival.WorldTransform.Position);
        }

        internal static NpcTransferSnapshot RebaseNpcSnapshotAtArrival(NpcTransferSnapshot snapshot,
            System.Numerics.Vector3 arrivalPosition)
        {
            if (string.IsNullOrWhiteSpace(snapshot.TargetArrivalObject))
                return snapshot;
            var leaderId = snapshot.Formations.SelectMany(formation => formation.Members)
                .FirstOrDefault(member => member.IsLeader && member.NpcId.HasValue)?.NpcId;
            var leader = snapshot.Npcs.FirstOrDefault(npc => npc.NpcId == leaderId) ?? snapshot.Npcs[0];
            var leaderState = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(leader.RuntimeState);
            var sourceLeaderPosition = new System.Numerics.Vector3(leaderState.Position.X, leaderState.Position.Y, leaderState.Position.Z);
            var offset = arrivalPosition - sourceLeaderPosition;
            var shifted = snapshot.Npcs.Select(identity =>
            {
                var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(identity.RuntimeState);
                var position = new System.Numerics.Vector3(state.Position.X, state.Position.Y, state.Position.Z) + offset;
                var rebased = new NpcRuntimeStateV1
                {
                    Position = new NpcVector3 { X = position.X, Y = position.Y, Z = position.Z },
                    Orientation = state.Orientation,
                    LinearVelocity = state.LinearVelocity,
                    AngularVelocity = state.AngularVelocity,
                    Health = state.Health,
                    LoadoutArchetype = state.LoadoutArchetype,
                    Equipment = state.Equipment,
                    Cargo = state.Cargo,
                    Autopilot = state.Autopilot,
                    Ai = state.Ai,
                    CurrentTargetNpcId = state.CurrentTargetNpcId,
                    MissionState = state.MissionState,
                    Nickname = state.Nickname,
                    DisplayName = state.DisplayName,
                    FactionId = state.FactionId,
                    PilotId = state.PilotId,
                    StateGraphId = state.StateGraphId,
                    CommHeadId = state.CommHeadId,
                    CommBodyId = state.CommBodyId,
                    CommAccessoryId = state.CommAccessoryId,
                    StructuralParts = state.StructuralParts
                };
                return new NpcRuntimeSnapshot
                {
                    NpcId = identity.NpcId,
                    OwnershipVersion = identity.OwnershipVersion,
                    SystemId = snapshot.TargetSystemId,
                    RuntimeSchemaVersion = identity.RuntimeSchemaVersion,
                    RuntimeState = MessagePackSerializer.Serialize(rebased)
                };
            }).ToArray();
            return new NpcTransferSnapshot
            {
                TransferId = snapshot.TransferId,
                NpcIds = snapshot.NpcIds,
                FormationId = snapshot.FormationId,
                MissionRuntimeId = snapshot.MissionRuntimeId,
                SnapshotSchemaVersion = snapshot.SnapshotSchemaVersion,
                Npcs = shifted,
                TargetSystemId = snapshot.TargetSystemId,
                TargetArrivalObject = snapshot.TargetArrivalObject,
                MissionRuntimeState = snapshot.MissionRuntimeState,
                Formations = snapshot.Formations
            };
        }

        private sealed record NpcTransferRecoveryWireRecord(Guid TransferId, string SourceInstanceId,
            string TargetInstanceId, string TargetSystemId, Guid[] NpcIds, DateTime ExpiresUtc,
            NpcTransferState State, string? SnapshotSha256, NpcTransferSnapshot? Snapshot = null);

    public NpcIdentityAllocator? GetNpcIdentityAllocator(string systemId)
    {
        if (string.IsNullOrWhiteSpace(InstanceId))
            return null;
        if (!Uri.TryCreate(NpcCoordinatorUrl, UriKind.Absolute, out var coordinatorUri) ||
            string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
            throw new InvalidOperationException("Coordinator NPC identity allocation is not configured.");
        lock (npcIdentityAllocators)
        {
            if (npcIdentityAllocators.TryGetValue(systemId, out var existing))
                return existing;
            var allocator = new NpcIdentityAllocator(coordinatorUri, npcCoordinatorApiKey, InstanceId, systemId);
            npcIdentityAllocators[systemId] = allocator;
            return allocator;
        }
    }

    public void ConfigureLocalOperators(string path)
    {
        // Any Gateway login configuration means central identity is expected; local OP must not bypass it.
        localOperators = string.IsNullOrWhiteSpace(LoginUrl) ? LocalOperatorStore.Load(path) : null;
    }

    public bool IsLocalOperator(Guid accountId) =>
        string.IsNullOrWhiteSpace(LoginUrl) && accountId != Guid.Empty && localOperators?.Contains(accountId) == true;

    public bool SetLocalOperator(Guid accountId, bool enabled)
    {
        if (!string.IsNullOrWhiteSpace(LoginUrl) || accountId == Guid.Empty || localOperators is null) return false;
        localOperators.Set(accountId, enabled);
        return true;
    }
        public bool ClusterTransfersEnabled => !string.IsNullOrWhiteSpace(InstanceId) &&
        !string.IsNullOrWhiteSpace(SystemId) && !string.IsNullOrWhiteSpace(TransferInstanceKey) &&
            !string.IsNullOrWhiteSpace(LoginUrl);

        public string CreateTargetTransferLeaseToken(Guid transferId)
        {
            if (!ClusterTransfersEnabled || transferId == Guid.Empty)
                throw new InvalidOperationException("Cluster transfer identity is not configured.");
            var key = Encoding.UTF8.GetBytes(TransferInstanceKey!);
            var payload = Encoding.UTF8.GetBytes($"target-transfer:{InstanceId}:{transferId:N}");
            return Convert.ToBase64String(HMACSHA256.HashData(key, payload))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    public int? TestNewCharacterRank { get; set; }
    public string? TestMissionNickname { get; set; }

    public const int MaxTransferSnapshotBytes = 16 * 1024 * 1024;
    private readonly HttpClient transferHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly ConcurrentDictionary<Guid, long> transferLeaseVersions = new();

        public async Task<LancerNexus.Protocol.AdminQueryResponse> QueryAdministrationAsync(
            LancerNexus.Protocol.GameAdminQueryRequest body)
        {
            if (!ClusterTransfersEnabled)
                throw new InvalidOperationException("Cluster administration is not configured.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                GetTransferGatewayUrl("/api/v1/game/admin/commands"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(body);
            using var response = await transferHttp.SendAsync(request, timeout.Token);
            var result = await response.Content.ReadFromJsonAsync<LancerNexus.Protocol.AdminQueryResponse>(timeout.Token);
            if (result == null || result.CorrelationId != body.Query.CorrelationId || result.Lines == null ||
                result.Lines.Length > 65 || result.Lines.Any(line => line == null || line.Length > 8192))
                throw new InvalidDataException("Administration returned an invalid response.");
            return result;
        }

        public async Task<PermissionCommandResponse> MutateAdministrationPermissionAsync(GamePermissionMutationRequest body)
        {
            if (!ClusterTransfersEnabled || body.AccountId == Guid.Empty ||
                (body.SessionId == Guid.Empty && body.TransferId == Guid.Empty) ||
                body.CharacterId <= 0 || body.Request.CorrelationId == Guid.Empty)
                throw new InvalidOperationException("Cluster permission administration is not configured.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                GetTransferGatewayUrl("/api/v1/game/admin/permissions/mutations"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(body);
            using var response = await transferHttp.SendAsync(request, timeout.Token);
            var result = await response.Content.ReadFromJsonAsync<PermissionCommandResponse>(timeout.Token);
            return result ?? new PermissionCommandResponse("unavailable", null);
        }

        public async Task<bool> HasCommandPermissionAsync(Player player, string permission)
        {
            if (string.IsNullOrWhiteSpace(LoginUrl))
                return IsLocalOperator(player.AccountId);
            if (!ClusterTransfersEnabled || player.AccountId == Guid.Empty || player.Character is null ||
                (player.GatewaySessionId == Guid.Empty && player.GatewayTransferId == Guid.Empty) ||
                permission is not { Length: > 0 and <= 256 } || !OwnsSystem(player.System)) return false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                GetTransferGatewayUrl("/api/v1/game/admin/permissions/check"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(new GamePermissionCheckRequest(player.AccountId,
                player.GatewaySessionId, player.Character.ID, player.GatewayTransferId, permission, player.System));
            using var response = await transferHttp.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<GamePermissionCheckResponse>(timeout.Token);
            return result?.Allowed == true;
        }

        public bool SendDebugInfo = false;
        public string DebugInfo { get; private set; } = null!;

        public string ScriptsFolder { get; set; } = null!;

        public int ThreadCount { get; set; } = Math.Min(1, (int)(Environment.ProcessorCount * 0.75));

        public IDesignTimeDbContextFactory<LibreLancerContext> DbContextFactory = null!;
        public GameDataManager GameData;
        public ServerDatabase Database = null!;
        public ResourceManager Resources;
        public WorldProvider Worlds = null!;
        public ServerPerformance PerformanceStats = null!;

        public BaselinePriceBundle BaselineGoodPrices;

        private volatile bool running = false;

        public GameListener? Listener;
        private Thread gameThread = null!;

        public List<Player> ConnectedPlayers = [];
        public Player? LocalPlayer;

        public ConcurrentHashSet<long> CharactersInUse = [];

        private bool needLoadData = true;

        private string debugInfoForFrame = "";
        public void ReportDebugInfo(string info)
        {
            debugInfoForFrame = info;
        }

        public GameServer(FileSystem vfs)
        {
            Resources = new ServerResourceManager(null, vfs);
            GameData = new GameDataManager(new GameItemDb(vfs), Resources);
            Listener = new GameListener(this);
        }

        public GameServer(GameDataManager gameData, ConvexShapeCollection convexCollection)
        {
            Resources = new ServerResourceManager(convexCollection, gameData.VFS);
            GameData = gameData;
            needLoadData = false;
        }

        public SaveGame NewCharacter(string name, int factionIndex)
        {
            var fac = GameData.Items.Ini.NewCharDB.Factions[factionIndex];
            var pilot = GameData.Items.Ini.NewCharDB.Pilots.First(x =>
                x.Nickname.Equals(fac.Pilot, StringComparison.OrdinalIgnoreCase));
            var package = GameData.Items.Ini.NewCharDB.Packages.First(x =>
                x.Nickname.Equals(fac.Package, StringComparison.OrdinalIgnoreCase));
            // TODO: initial_rep = %%FACTION%%
            // does this have any effect in FL?

            var src = new StringBuilder(Encoding.UTF8.GetString(FlCodec.DecodeBytes(
                GameData.VFS.ReadAllBytes(GameData.Items.Ini.Freelancer.MpNewCharacterPath))));

            src.Replace("%%NAME%%", SavePlayer.EncodeName(name));
            src.Replace("%%BASE_COSTUME%%", pilot.Body);
            src.Replace("%%COMM_COSTUME%%", pilot.Comm);
            // Changing voice breaks in vanilla (commented out in mpnewcharacter)
            src.Replace("%%VOICE%%", pilot.Voice);
            // TODO: pilot comm_anim (not in vanilla mpnewcharacter)
            // TODO: pilot body_anim (not in vanilla mpnewcharacter)
            src.Replace("%%MONEY%%", package.Money.ToString());
            src.Replace("%%HOME_SYSTEM%%", GameData.Items.Bases.Get(fac.Base)!.System);
            src.Replace("%%HOME_BASE%%", fac.Base);

            var pkgStr = new StringBuilder();
            pkgStr.Append("ship_archetype = ").AppendLine(package.Ship);
            var loadout = GameData.Items.Ini.Loadouts.Loadouts.First(x =>
                x.Nickname.Equals(package.Loadout, StringComparison.OrdinalIgnoreCase));
            // do loadout
            foreach (var x in loadout.Equip)
            {
                pkgStr.AppendLine(new PlayerEquipment()
                {
                    Item = new HashValue(x.Nickname),
                    Hardpoint = x.Hardpoint ?? ""
                }.ToString());
            }

            foreach (var x in loadout.Cargo)
            {
                pkgStr.AppendLine(new PlayerCargo()
                {
                    Item = new HashValue(x.Nickname!),
                    Count = x.Count
                }.ToString());
            }

            // append
            src.Replace("%%PACKAGE%%", pkgStr.ToString());
            var initext = src.ToString();
            var newCharacter = SaveGame.FromString($"mpnewcharacter: {fac.Nickname}", initext);
            if (TestNewCharacterRank is int testRank && newCharacter.Player is not null)
                newCharacter.Player.Rank = testRank;
            return newCharacter;
        }

        public void Start()
        {
            running = true;
            gameThread = new Thread(GameThread)
            {
                Name = "Game Server"
            };
            gameThread.Start();
        }

        public void JoinThread()
        {
            gameThread.Join();
        }

        public void Stop()
        {
            running = false;
            gameThread.Join();
            foreach (var allocator in npcIdentityAllocators.Values)
                allocator.Dispose();
            npcIdentityAllocators.Clear();
            npcRetirementOutbox?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            npcCheckpointOutbox?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            npcTransferHttp.Dispose();
            updateRunner.Dispose();
        }

        private Dictionary<StarSystem, ServerWorld> worlds = new();
        private ConcurrentQueue<Action> worldRequests = new();
        private ConcurrentQueue<IPacket> localPackets = new();
        public readonly ConcurrentQueue<ServerEvent> ServerEvents = new();

        public void OnLocalPacket(IPacket pkt)
        {
            localPackets.Enqueue(pkt);
        }

        public void WorldReady(ServerWorld world)
        {
            worldRequests.Enqueue(() =>
            {
                var sysName = this.GameData.GetString(world.System.IdsName);
                FLLog.Info("Server", "Spun up " + world.System.Nickname + " (" + sysName + ")");
                worlds.Add(world.System, world);
            });
        }

        private void InitBaselinePrices()
        {
            var bp = new List<BaselinePrice>();
            foreach (var good in GameData.Items.Goods)
            {
                bp.Add(new BaselinePrice()
                {
                    GoodCRC = FLHash.CreateID(good.Ini.Nickname),
                    Price = (ulong) good.Ini.Price
                });
            }

            if (Listener == null)
            {
                BaselineGoodPrices = new BaselinePriceBundle() { Prices = bp.ToArray() };
            }
            else
            {
                BaselineGoodPrices = BaselinePriceBundle.Compress(bp.ToArray());
            }
        }

        public void SystemChatMessage(Player source, BinaryChatMessage message)
        {
            var s = source.System;
            foreach (var p in GetConnectedPlayers())
            {
                if (p.System.Equals(s, StringComparison.OrdinalIgnoreCase))
                    p.RpcClient.ReceiveChatMessage(ChatCategory.System, BinaryChatMessage.PlainText(source.Name+ ": "), message);
            }
        }

        private IEnumerable<Player> GetConnectedPlayers()
        {
            lock (ConnectedPlayers)
            {
                return ConnectedPlayers.ToArray();
            }
        }

        public IEnumerable<Player> AllPlayers => GetConnectedPlayers();

        public Player? GetConnectedPlayer(string name) =>
            GetConnectedPlayers().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Freezes a connected character and captures its save snapshot. The caller must first validate
        /// the transfer with Gateway and may only release or abort it using the matching authoritative result.
        /// </summary>
        public Task<byte[]> FreezeCharacterForTransferAsync(Guid transferId, long characterId,
            string? targetSystem = null, string? target = null)
        {
            if (transferId == Guid.Empty || characterId <= 0)
                throw new ArgumentException("Transfer and character IDs must be valid.");
            var player = GetConnectedPlayers().SingleOrDefault(x => x.Character?.ID == characterId);
            if (player == null)
                return Task.FromException<byte[]>(new InvalidOperationException("Character is not connected to this instance."));
            return player.FreezeForTransferAsync(transferId, characterId, targetSystem, target);
        }

        /// <summary>
        /// Freezes the source player and durably stages its save through Gateway before the source is
        /// announced as frozen. On a transport failure the player remains frozen until authoritative
        /// transfer status is checked; the caller must not resume it based on a timeout alone.
        /// </summary>
        public async Task StageCharacterTransferSnapshotAsync(Guid transferId, long characterId, long leaseVersion,
            string targetSystem, string target,
            CancellationToken cancellationToken = default)
        {
            if (leaseVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(leaseVersion));
            var gatewayUrl = GetTransferGatewayUrl($"/api/v1/game/freeze-transfer/{transferId:D}");
            if (!transferLeaseVersions.TryAdd(transferId, leaseVersion) &&
                (!transferLeaseVersions.TryGetValue(transferId, out var knownVersion) || knownVersion != leaseVersion))
                throw new InvalidOperationException("Transfer ID is already associated with a different source lease.");
            byte[] snapshot;
            try
            {
                snapshot = await FreezeCharacterForTransferAsync(transferId, characterId, targetSystem, target)
                    .ConfigureAwait(false);
            }
            catch
            {
                if (!GetConnectedPlayers().Any(x => x.PendingTransfer?.TransferId == transferId))
                    transferLeaseVersions.TryRemove(transferId, out _);
                throw;
            }
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, gatewayUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
                request.Headers.Add("X-Lancer-Nexus-Lease-Version", leaseVersion.ToString(CultureInfo.InvariantCulture));
                request.Content = new ByteArrayContent(snapshot);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                using var response = await transferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Gateway snapshot staging failed with HTTP {(int)response.StatusCode}.");
                var result = await response.Content.ReadFromJsonAsync<TransferSnapshotStagedResponse>(
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result is null || result.TransferId != transferId || result.SnapshotBytes != snapshot.Length ||
                    !string.Equals(result.InstanceId, InstanceId, StringComparison.Ordinal) ||
                    !string.Equals(result.State, "SourceFrozen", StringComparison.Ordinal))
                    throw new InvalidDataException("Gateway snapshot staging response is invalid.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(snapshot);
            }
        }

        /// <summary>Fetches a staged snapshot for the authenticated target instance.</summary>
        public async Task<byte[]> DownloadTransferSnapshotAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            if (transferId == Guid.Empty)
                throw new ArgumentException("Transfer ID must be valid.", nameof(transferId));
            var gatewayUrl = GetTransferGatewayUrl($"/api/v1/game/transfers/{transferId:D}/snapshot");
            using var request = new HttpRequestMessage(HttpMethod.Get, gatewayUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            using var response = await transferHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gateway snapshot retrieval failed with HTTP {(int)response.StatusCode}.");
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/octet-stream",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Gateway transfer snapshot has an invalid content type.");
            if (response.Content.Headers.ContentLength is > MaxTransferSnapshotBytes)
                throw new InvalidDataException("Gateway transfer snapshot exceeds the supported limit.");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (output.Length > MaxTransferSnapshotBytes - read)
                    throw new InvalidDataException("Gateway transfer snapshot exceeds the supported limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (output.Length == 0)
                throw new InvalidDataException("Gateway returned an empty transfer snapshot.");
            return output.ToArray();
        }

        /// <summary>
        /// Verifies the target ticket, durably imports the source save into the local database,
        /// then asks Gateway to accept and commit the transfer. Repeated calls are idempotent.
        /// </summary>
        public async Task<TransferSnapshotTargetReadyResult> ImportTransferSnapshotAndAcceptAsync(
            Guid transferId, string transferTicket, string targetLeaseToken,
            CancellationToken cancellationToken = default)
        {
            if (transferId == Guid.Empty || string.IsNullOrWhiteSpace(transferTicket) ||
                string.IsNullOrWhiteSpace(targetLeaseToken))
                throw new ArgumentException("Transfer acceptance data is incomplete.");

            var status = await GetTransferStatusAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(status.TargetInstanceId, InstanceId, StringComparison.Ordinal))
                throw new InvalidDataException("Transfer is assigned to another target instance.");
            if (!OwnsSystem(status.TargetSystemId))
                throw new InvalidDataException("Transfer is assigned to another target system.");

            var ticketHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transferTicket)));
            var import = await Database.GetTransferSnapshotImportAsync(transferId).ConfigureAwait(false);
            SaveGame? transferSave = null;
            var alreadyCompleted = status.State == LancerNexus.Protocol.TransferState.SourceReleased;
            if (status.State == LancerNexus.Protocol.TransferState.SourceFrozen)
            {
                if (import == null)
                {
                    var claims = await VerifyTransferTicketAsync(transferId, transferTicket, cancellationToken)
                        .ConfigureAwait(false);
                    if (claims.TransferId != transferId || claims.Guid == Guid.Empty || claims.CharacterId <= 0 ||
                        // Status.LeaseVersion is the committed version; before commit it is unset (0).
                        // Gateway verifies the signed source version and fences the atomic lease commit.
                        claims.LeaseVersion < 0 ||
                        !string.Equals(claims.InstanceId, InstanceId, StringComparison.Ordinal) ||
                        !string.Equals(claims.SystemId, status.TargetSystemId, StringComparison.Ordinal) ||
                        !string.Equals(claims.SourceInstanceId, status.SourceInstanceId, StringComparison.Ordinal))
                        throw new InvalidDataException("Gateway transfer ticket claims do not match the target transfer.");

                    await Database.RecordTransferSnapshotImportAsync(transferId, claims.Guid, claims.CharacterId,
                        claims.SourceInstanceId, claims.InstanceId, claims.SystemId, claims.LeaseVersion, ticketHash)
                        .ConfigureAwait(false);
                    import = await Database.GetTransferSnapshotImportAsync(transferId).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Transfer import could not be recorded durably.");
                }
                else if (!string.Equals(import.TicketHash, ticketHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Transfer retry supplied a different ticket.");
                }

                if (!import.Imported)
                {
                    var snapshot = await DownloadTransferSnapshotAsync(transferId, cancellationToken)
                        .ConfigureAwait(false);
                    try
                    {
                        var snapshotHash = Convert.ToHexString(SHA256.HashData(snapshot));
                        var text = new UTF8Encoding(false, true).GetString(snapshot);
                        var save = SaveGame.FromString($"transfer-{transferId:N}.fl", text);
                        transferSave = save;
                        await Database.ApplyTransferSnapshotImportAsync(transferId, snapshotHash, save)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(snapshot);
                    }
                }
            }
            else if (status.State is LancerNexus.Protocol.TransferState.TargetAccepted or
                     LancerNexus.Protocol.TransferState.Committed)
            {
                if (import is not { Imported: true } ||
                    !string.Equals(import.TicketHash, ticketHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Transfer cannot be accepted before its snapshot is durably imported.");
            }
            else if (status.State == LancerNexus.Protocol.TransferState.SourceReleased)
            {
                if (import is not { Imported: true } ||
                    !string.Equals(import.TicketHash, ticketHash, StringComparison.Ordinal) ||
                    status.LeaseVersion != import.LeaseVersion + 1)
                    throw new InvalidOperationException("Released transfer has no matching committed target import.");
            }
            else
            {
                throw new InvalidOperationException($"Transfer cannot be imported in state {status.State}.");
            }

            import = await Database.GetTransferSnapshotImportAsync(transferId).ConfigureAwait(false);
            if (import is not { Imported: true } ||
                !string.Equals(import.TicketHash, ticketHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Transfer snapshot import did not reach its durable state.");

            await ValidatePreparedNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);

            if (transferSave is null)
            {
                var snapshot = await DownloadTransferSnapshotAsync(transferId, cancellationToken).ConfigureAwait(false);
                try
                {
                    var snapshotHash = Convert.ToHexString(SHA256.HashData(snapshot));
                    if (!string.Equals(import.SnapshotHash, snapshotHash, StringComparison.Ordinal))
                        throw new InvalidDataException("Gateway transfer snapshot changed after it was imported.");
                    var text = new UTF8Encoding(false, true).GetString(snapshot);
                    transferSave = SaveGame.FromString($"transfer-{transferId:N}.fl", text);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(snapshot);
                }
            }

            var acceptedLeaseVersion = status.LeaseVersion;
            if (!alreadyCompleted)
            {
                var accepted = await AcceptTransferAsync(transferTicket, targetLeaseToken, cancellationToken)
                    .ConfigureAwait(false);
                if (accepted.TransferId != transferId ||
                !string.Equals(accepted.InstanceId, InstanceId, StringComparison.Ordinal) ||
                accepted.LeaseVersion != import.LeaseVersion + 1)
                    throw new InvalidDataException("Gateway transfer commit response does not match the imported snapshot.");
                acceptedLeaseVersion = accepted.LeaseVersion;
            }
            var npcSnapshot = await WaitForNpcTransferCommitAsync(transferId, cancellationToken)
                .ConfigureAwait(false);
            var acceptedPlayer = GetConnectedPlayers().SingleOrDefault(player => player.Character?.ID == import.CharacterId);
            if (acceptedPlayer != null)
                acceptedPlayer.GatewayTransferId = transferId;
            return new TransferSnapshotTargetReadyResult(transferId, import.AccountId, import.CharacterId,
                acceptedLeaseVersion, alreadyCompleted, transferSave, npcSnapshot);
        }

        private async Task<byte[]> ReadCommittedNpcSnapshotPayloadAsync(Guid transferId,
            NpcTransferRecoveryWireRecord recovery, string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(recovery.SnapshotSha256))
                throw new InvalidDataException("Committed NPC transfer has no Coordinator snapshot hash.");
            byte[] expectedHash;
            try { expectedHash = Convert.FromHexString(recovery.SnapshotSha256); }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Committed NPC transfer snapshot hash is malformed.", exception);
            }
            if (expectedHash.Length != SHA256.HashSizeInBytes)
                throw new InvalidDataException("Committed NPC transfer snapshot hash has an invalid size.");

            byte[] payload = File.Exists(path)
                ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                : [];
            if (CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(payload)))
                return payload;

            var authoritative = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken, includeSnapshot: true)
                .ConfigureAwait(false);
            if (authoritative is not
                {
                    Snapshot: { } snapshot,
                    State: NpcTransferState.Committed or NpcTransferState.SourceReleased
                } || authoritative.TransferId != transferId || authoritative.TargetInstanceId != InstanceId ||
                !string.Equals(authoritative.SnapshotSha256, recovery.SnapshotSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(authoritative.TargetSystemId, recovery.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                !authoritative.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("Committed NPC snapshot is missing or inconsistent in Coordinator recovery.");
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.TransferId != transferId ||
                !string.Equals(snapshot.TargetSystemId, recovery.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                !snapshot.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("Coordinator recovery snapshot does not match the committed transfer.");

            payload = MessagePackSerializer.Serialize(snapshot);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(payload)))
                throw new InvalidDataException("Coordinator recovery snapshot does not match its journal hash.");
            await WriteNpcSnapshotAtomicallyAsync(path, payload, cancellationToken, replaceExisting: true)
                .ConfigureAwait(false);
            return payload;
        }

        private async Task<NpcTransferSnapshot?> WaitForNpcTransferCommitAsync(Guid transferId,
            CancellationToken cancellationToken)
        {
            var stagedPath = string.IsNullOrWhiteSpace(NpcTransferStagingDirectory)
                ? null
                : Path.Combine(NpcTransferStagingDirectory, $"{transferId:N}.msgpack");
            var npcTransferRequired = stagedPath is not null && File.Exists(stagedPath);
            if (string.IsNullOrWhiteSpace(NpcCoordinatorUrl) || string.IsNullOrWhiteSpace(npcCoordinatorApiKey) ||
                string.IsNullOrWhiteSpace(NpcTransferStagingDirectory))
            {
                if (npcTransferRequired)
                    throw new InvalidOperationException("A staged NPC transfer has no Coordinator recovery connection.");
                return null;
            }
            if (IsNpcTransferActivated(transferId))
                return null;
            while (true)
            {
                var recovery = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
                if (recovery is null)
                {
                    if (npcTransferRequired)
                        throw new InvalidDataException("The staged NPC transfer is missing its Coordinator journal record.");
                    return null;
                }
                if (recovery.State == NpcTransferState.Aborted)
                    throw new InvalidOperationException("NPC transfer was aborted after the character lease committed.");
                if (recovery.State == NpcTransferState.TargetAccepted &&
                    await TryCommitTargetAcceptedMissionNpcTransferAsync(transferId, recovery, cancellationToken)
                        .ConfigureAwait(false))
                    continue;
                if (recovery.State is NpcTransferState.Committed or NpcTransferState.SourceReleased)
                {
                    if (recovery.TargetInstanceId != InstanceId || !OwnsSystem(recovery.TargetSystemId))
                        throw new InvalidDataException("Committed NPC transfer belongs to another target.");
                    var path = Path.Combine(NpcTransferStagingDirectory, $"{transferId:N}.msgpack");
                    var payload = await ReadCommittedNpcSnapshotPayloadAsync(transferId, recovery, path,
                        cancellationToken).ConfigureAwait(false);
                    var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(payload,
                        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
                    NpcTransferContractValidator.Validate(snapshot);
                    if (snapshot.TransferId != transferId ||
                        !string.Equals(snapshot.TargetSystemId, recovery.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                        !snapshot.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                        throw new InvalidDataException("Committed NPC snapshot identity does not match its Coordinator record.");
                    return snapshot;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ValidatePreparedNpcTransferAsync(Guid transferId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(NpcTransferStagingDirectory))
                return;
            var path = Path.Combine(NpcTransferStagingDirectory, $"{transferId:N}.msgpack");
            if (!File.Exists(path))
                return;
            if (string.IsNullOrWhiteSpace(NpcCoordinatorUrl) || string.IsNullOrWhiteSpace(npcCoordinatorApiKey))
                throw new InvalidOperationException("A staged NPC transfer has no Coordinator recovery connection.");

            var recovery = await ReadNpcTransferRecoveryAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (recovery is null || recovery.TransferId != transferId || recovery.TargetInstanceId != InstanceId ||
                recovery.State is not (NpcTransferState.TargetAccepted or NpcTransferState.Committed or NpcTransferState.SourceReleased) ||
                string.IsNullOrWhiteSpace(recovery.SnapshotSha256))
                throw new InvalidDataException("The staged NPC transfer is not durably accepted by the Coordinator.");

            var payload = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            byte[] expectedHash;
            try { expectedHash = Convert.FromHexString(recovery.SnapshotSha256); }
            catch (FormatException exception)
            {
                throw new InvalidDataException("The Coordinator NPC snapshot hash is malformed.", exception);
            }
            if (expectedHash.Length != SHA256.HashSizeInBytes ||
                !CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(payload)))
                throw new InvalidDataException("The staged NPC snapshot does not match the Coordinator journal.");

            var snapshot = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(payload,
                MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            NpcTransferContractValidator.Validate(snapshot);
            if (snapshot.TransferId != transferId ||
                !string.Equals(snapshot.TargetSystemId, recovery.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                !snapshot.NpcIds.Order().SequenceEqual(recovery.NpcIds.Order()))
                throw new InvalidDataException("The staged NPC snapshot identity does not match the Coordinator journal.");
        }

        private async Task<TransferTicketVerificationResponse> VerifyTransferTicketAsync(
            Guid transferId, string ticket, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                GetTransferGatewayUrl("/api/v1/game/verify-transfer-ticket"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(new TransferTicketVerificationRequestBody(ticket, InstanceId!));
            using var response = await transferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gateway transfer ticket verification failed with HTTP {(int)response.StatusCode}.");
            var claims = await response.Content.ReadFromJsonAsync<TransferTicketVerificationResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return claims is { TransferId: var verifiedId } && verifiedId == transferId
                ? claims
                : throw new InvalidDataException("Gateway transfer ticket verification response is invalid.");
        }

        private async Task<TransferAcceptanceResponse> AcceptTransferAsync(
            string ticket, string targetLeaseToken, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                GetTransferGatewayUrl("/api/v1/game/accept-transfer"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(new TransferTargetAcceptanceRequestBody(ticket, targetLeaseToken));
            using var response = await transferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gateway transfer acceptance failed with HTTP {(int)response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<TransferAcceptanceResponse>(
                       cancellationToken: cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidDataException("Gateway transfer acceptance response is empty.");
        }

        public async Task<LancerNexus.Protocol.TransferStatusResponse> GetTransferStatusAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            if (transferId == Guid.Empty)
                throw new ArgumentException("Transfer ID must be valid.", nameof(transferId));
            var gatewayUrl = GetTransferGatewayUrl($"/api/v1/game/transfers/{transferId:D}");
            using var request = new HttpRequestMessage(HttpMethod.Get, gatewayUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            using var response = await transferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gateway transfer status failed with HTTP {(int)response.StatusCode}.");
            var status = await response.Content.ReadFromJsonAsync<LancerNexus.Protocol.TransferStatusResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (status is null || status.TransferId != transferId ||
                (!string.Equals(status.SourceInstanceId, InstanceId, StringComparison.Ordinal) &&
                 !string.Equals(status.TargetInstanceId, InstanceId, StringComparison.Ordinal)))
                throw new InvalidDataException("Gateway transfer status response is invalid.");
            return status;
        }

        public async Task NotifyTransferSourceReleasedAsync(Guid transferId,
            CancellationToken cancellationToken = default)
        {
            if (transferId == Guid.Empty)
                throw new ArgumentException("Transfer ID must be valid.", nameof(transferId));
            var gatewayUrl = GetTransferGatewayUrl("/api/v1/game/release-transfer");
            using var request = new HttpRequestMessage(HttpMethod.Post, gatewayUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TransferInstanceKey!);
            request.Content = JsonContent.Create(new TransferSourceReleaseRequestBody(transferId));
            using var response = await transferHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Gateway transfer source release failed with HTTP {(int)response.StatusCode}.");
            var result = await response.Content.ReadFromJsonAsync<TransferSourceReleasedResponse>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result is null || result.TransferId != transferId ||
                !string.Equals(result.InstanceId, InstanceId, StringComparison.Ordinal) ||
                !string.Equals(result.State, "SourceReleased", StringComparison.Ordinal))
                throw new InvalidDataException("Gateway transfer source release response is invalid.");
        }

        /// <summary>
        /// Resolves the source side of a transfer from Gateway's authoritative state. A committed
        /// transfer releases the frozen source character before notifying Gateway; an aborted transfer
        /// resumes it. Calling this repeatedly is safe after either result.
        /// </summary>
        public async Task<TransferSourceResolutionResult> ResolveSourceTransferAsync(
            Guid transferId, long characterId, long sourceLeaseVersion,
            CancellationToken cancellationToken = default)
        {
            if (transferId == Guid.Empty || characterId <= 0 || sourceLeaseVersion < 0)
                throw new ArgumentException("Transfer resolution data is invalid.");

            var status = await GetTransferStatusAsync(transferId, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(status.SourceInstanceId, InstanceId, StringComparison.Ordinal))
                throw new InvalidDataException("Transfer belongs to another source instance.");

            var sourcePlayer = GetConnectedPlayers().SingleOrDefault(x => x.Character?.ID == characterId);
            switch (status.State)
            {
                case LancerNexus.Protocol.TransferState.Committed:
                case LancerNexus.Protocol.TransferState.SourceReleased:
                    if (sourceLeaseVersion == long.MaxValue || status.LeaseVersion != sourceLeaseVersion + 1)
                        throw new InvalidDataException("Committed transfer lease version does not match the source lease.");
                    await CommitNpcTransferAsync(transferId, cancellationToken).ConfigureAwait(false);
                    if (status.State == LancerNexus.Protocol.TransferState.Committed)
                    {
                        if (sourcePlayer != null && !sourcePlayer.ReleaseAfterTransferCommit(transferId))
                            throw new InvalidOperationException("Source character is not frozen for this transfer.");
                        await NotifyTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                        await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                        transferLeaseVersions.TryRemove(transferId, out _);
                        return new TransferSourceResolutionResult(transferId,
                            LancerNexus.Protocol.TransferState.SourceReleased, true);
                    }
                    if (sourcePlayer != null && !sourcePlayer.ReleaseAfterTransferCommit(transferId))
                        throw new InvalidOperationException("Source character is not frozen for this transfer.");
                    await MarkNpcTransferSourceReleasedAsync(transferId, cancellationToken).ConfigureAwait(false);
                    transferLeaseVersions.TryRemove(transferId, out _);
                    return new TransferSourceResolutionResult(transferId, status.State, true);

                case LancerNexus.Protocol.TransferState.Rejected:
                case LancerNexus.Protocol.TransferState.Expired:
                case LancerNexus.Protocol.TransferState.Aborted:
                case LancerNexus.Protocol.TransferState.TimedOut:
                    await AbortNpcTransferAndRestoreAsync(transferId, sourcePlayer?.Space?.World,
                        sourcePlayer?.MissionRuntime, cancellationToken).ConfigureAwait(false);
                    if (sourcePlayer?.TransferInProgress == true && !sourcePlayer.AbortTransferFreeze(transferId))
                        throw new InvalidOperationException("Source character is not frozen for this transfer.");
                    transferLeaseVersions.TryRemove(transferId, out _);
                    return new TransferSourceResolutionResult(transferId, status.State, true);

                default:
                    return new TransferSourceResolutionResult(transferId, status.State, false);
            }
        }

        /// <summary>Checks every frozen source transfer against Gateway and resolves committed or aborted transfers.</summary>
        public async Task ResolvePendingSourceTransfersAsync(CancellationToken cancellationToken = default)
        {
            foreach (var player in GetConnectedPlayers())
            {
                var pending = player.PendingTransfer;
                if (pending is not { } transfer ||
                    !transferLeaseVersions.TryGetValue(transfer.TransferId, out var leaseVersion))
                    continue;
                try
                {
                    await ResolveSourceTransferAsync(transfer.TransferId, transfer.CharacterId, leaseVersion,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    FLLog.Warning("Transfer", $"Could not resolve transfer {transfer.TransferId}: {exception.Message}");
                }
            }
        }

        private Uri GetTransferGatewayUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(LoginUrl) || !Uri.TryCreate(LoginUrl, UriKind.Absolute, out var gateway) ||
                gateway.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(gateway.UserInfo) ||
                !string.IsNullOrEmpty(gateway.Query) || !string.IsNullOrEmpty(gateway.Fragment))
                throw new InvalidOperationException("An HTTPS Gateway URL is required for character transfers.");
            if (string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(TransferInstanceKey) ||
                TransferInstanceKey.Length < 32)
                throw new InvalidOperationException("Gateway instance identity and transfer key are required.");
            return new Uri(LoginUrl.TrimEnd('/') + path, UriKind.Absolute);
        }

        private sealed record TransferSnapshotStagedResponse(
            Guid TransferId, string InstanceId, string State, int SnapshotBytes);
        private sealed record TransferTicketVerificationRequestBody(string Ticket, string TargetInstanceId);
        private sealed record TransferTicketVerificationResponse(
            Guid Guid, Guid SessionId, Guid TransferId, long CharacterId, string SourceInstanceId,
            string InstanceId, string SystemId, long LeaseVersion);
        private sealed record TransferTargetAcceptanceRequestBody(string Ticket, string TargetLeaseToken);
        private sealed record TransferAcceptanceResponse(Guid TransferId, string InstanceId, long LeaseVersion);
        private sealed record TransferSourceReleaseRequestBody(Guid TransferId);
        private sealed record TransferSourceReleasedResponse(Guid TransferId, string InstanceId, string State);

        public bool AbortCharacterTransfer(Guid transferId, long characterId) =>
            GetConnectedPlayers().SingleOrDefault(x => x.Character?.ID == characterId)?.AbortTransferFreeze(transferId) ?? false;

        public bool ReleaseCharacterAfterTransferCommit(Guid transferId, long characterId) =>
            GetConnectedPlayers().SingleOrDefault(x => x.Character?.ID == characterId)?.ReleaseAfterTransferCommit(transferId) ?? false;

#if DEBUG
        /// <summary>Read-only local diagnostics captured between simulation updates.</summary>
        public Task<string> CaptureNpcDiagnosticsAsync(Guid? npcId = null)
        {
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            worldRequests.Enqueue(() =>
            {
                try
                {
                    var npcs = worlds.Values.SelectMany(world => world.GameWorld.Objects
                        .Where(obj => (obj.Flags & LibreLancer.World.GameObjectFlags.Exists) != 0 &&
                            obj.TryGetComponent<Components.SNPCComponent>(out var npc) &&
                            npc.NpcId != Guid.Empty && (!npcId.HasValue || npc.NpcId == npcId))
                        .Select(obj =>
                        {
                            var npc = obj.GetComponent<Components.SNPCComponent>()!;
                            var position = obj.WorldTransform.Position;
                            return new
                            {
                                npc.NpcId, npc.OwnershipVersion, obj.Nickname,
                                SystemId = world.System.Nickname,
                                Position = new { position.X, position.Y, position.Z },
                                FormationLeaderId = obj.Formation?.LeadShip?
                                    .GetComponent<Components.SNPCComponent>()?.NpcId,
                                MissionState = npc.MissionRuntime?.CaptureTransferState()
                            };
                        })).OrderBy(npc => npc.NpcId).ToArray();
                    result.TrySetResult(JsonSerializer.Serialize(new { InstanceId, CurrentTick, Npcs = npcs }));
                }
                catch (Exception exception) { result.TrySetException(exception); }
            });
            return result.Task;
        }
#endif

        public void LoadSaveGame(SaveGame sg) => worldRequests.Enqueue(() =>
        {
            LocalPlayer!.OpenSaveGame(sg);
        });

        private FixedTimestepLoop processingLoop = null!;

        public double TotalTime => processingLoop.TotalTime.TotalSeconds;

        public uint CurrentTick { get; private set; }

        private int slowCount = 0;
        private ParallelActionRunner updateRunner = null!;

        private void Process(TimeSpan time, TimeSpan totalTime, uint currentTick, int step)
        {
            CurrentTick = currentTick;
            var startTime = serverTiming.Elapsed;
            while (!localPackets.IsEmpty && localPackets.TryDequeue(out var local))
                LocalPlayer!.ProcessPacketDirect(local);
            if (worldRequests.Count > 0 && worldRequests.TryDequeue(out var a))
                a();
            // Update
            var connectedPlayers = GetConnectedPlayers();
            foreach (var player in connectedPlayers)
            {
                if (!player.TransferInProgress && !(player.Space?.World?.Paused ?? false))
                    player.UpdateMissionRuntime(time.TotalSeconds);
            }
            if (LocalPlayer is { } localPlayer && !connectedPlayers.Contains(localPlayer) &&
                !localPlayer.TransferInProgress &&
                !(localPlayer.Space?.World?.Paused ?? false))
                localPlayer.UpdateMissionRuntime(time.TotalSeconds);
            LocalPlayer?.RunSave();
            debugInfoForFrame = "";

            // Copy worlds to array
            var allSystems = worlds.ToArray();
            // Can't be stack allocated as we reference from multiple threads
            bool[] shouldSpinDown = ArrayPool<bool>.Shared.Rent(allSystems.Length);

            void Worker(int jobIndex)
            {
                shouldSpinDown[jobIndex] =
                    !allSystems[jobIndex].Value.Update(
                        time.TotalSeconds,
                        totalTime.TotalSeconds,
                        currentTick, step);
            }
            updateRunner.RunActions(Worker, allSystems.Length);

            DebugInfo = debugInfoForFrame;
            Listener?.Server?.TriggerUpdate(); // Send packets asap
            // Remove
            for (int i = 0; i < allSystems.Length; i++)
            {
                var w = allSystems[i];
                if (shouldSpinDown[i] && w.Value.PlayerCount <= 0)
                {
                    Worlds.RemoveWorld(w.Key);
                    worlds.Remove(w.Key);
                    var wName = GameData.GetString(w.Key.IdsName);
                    Task.Run(() => w.Value.Finish()); // this does a lot of checks in debug for held objects, unimportant.
                    FLLog.Info("Server", $"Shut down world {w.Key.Nickname} ({wName})");
                }
            }
            ArrayPool<bool>.Shared.Return(shouldSpinDown);

            var updateDuration = serverTiming.Elapsed - startTime;
            PerformanceStats?.AddEntry((float)updateDuration.TotalMilliseconds);
            bool slow = (updateDuration > TimeSpan.FromTicks(166667));
            if (slowCount == 0 && slow)
            {
                FLLog.Warning("Server", $"Running slow: update took {updateDuration.TotalMilliseconds:F2}ms");
                slowCount++;
            }
            else if (slow)
            {
                slowCount++;
                if (slowCount > 360)
                {
                    FLLog.Warning("Server", $"Still running slow: update took {updateDuration.TotalMilliseconds:F2}ms");
                    slowCount = 1;
                }
            }
            else if (slowCount > 0 && !slow)
            {
                FLLog.Info("Server", $"Not running slow: update took {updateDuration.TotalMilliseconds:F2}ms");
                slowCount = 0;
            }
            if (!running) processingLoop.Stop();
        }

        private Stopwatch serverTiming = null!;

        private void GameThread()
        {
            if (needLoadData)
            {
                LuaHardwire_LibreLancer.Initialize();
                FLLog.Info("Server", "Loading Game Data...");
                GameData.LoadData(null);
                FLLog.Info("Server", "Finished Loading Game Data");
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }
            Task.Run(() => PhysicsWarmup.Warmup());
            InitBaselinePrices();
            Worlds = new WorldProvider(this);
            serverTiming = Stopwatch.StartNew();
            Database = new ServerDatabase(this);
            Listener?.Start();
            updateRunner = new ParallelActionRunner(ThreadCount, "Server Update Worker");
            processingLoop = new FixedTimestepLoop(Process);
            processingLoop.Start();
            Listener?.Stop();
            Database.Dispose();
            Database = null!;
        }
    }

    public sealed record TransferSnapshotTargetReadyResult(
        Guid TransferId, Guid AccountId, long CharacterId, long LeaseVersion, bool AlreadyCompleted,
        SaveGame SaveGame, NpcTransferSnapshot? NpcSnapshot);

    public sealed record TransferSourceResolutionResult(
        Guid TransferId, LancerNexus.Protocol.TransferState State, bool Completed);
}
