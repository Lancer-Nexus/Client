using System;
using LibreLancer.World;
using LibreLancer.Missions;

namespace LibreLancer.Server.Components;

public class SDestroyableComponent : GameComponent
{
    public ServerWorld Server;
    public Action? OnKilled;
    private bool terminalCheckpointPending;

    public SDestroyableComponent(GameObject parent, ServerWorld server, MissionRuntime? missionRuntime = null) : base(parent)
    {
        Server = server;
        if (missionRuntime != null)
        {
            // Both fresh spawns and transfer restores pass through this constructor.
            var nickname = parent.Nickname!;
            OnKilled = () => missionRuntime.ObjectDestroyed(nickname);
        }
    }

    public void Destroy(bool exploded)
    {
        if (terminalCheckpointPending) return;
        if (Parent.TryGetComponent<SNPCComponent>(out var activeNpc) && activeNpc.TerminalCheckpointPending)
            return;
        if (!Parent.TryGetComponent<SPlayerComponent>(out _) &&
            Server is { Server: { } gameServer } &&
            gameServer.TryQueueNpcTerminalCheckpoint(Server, Parent,
                LancerNexus.Protocol.NpcRetirementReasonV1.Destroyed, () => FinalizeDestroy(exploded)))
        {
            terminalCheckpointPending = true;
            if (Parent.TryGetComponent<SNPCComponent>(out var npc))
                npc.TerminalCheckpointPending = true;
            return;
        }

        FinalizeDestroy(exploded);
    }

    private void FinalizeDestroy(bool exploded)
    {
        if (Parent.Formation?.Contains(Parent) == true)
            Parent.Formation.Remove(Parent);

        OnKilled?.Invoke();
        if (Parent.TryGetComponent<SPlayerComponent>(out var player))
        {
            player.Killed();
        }
        else
        {
            Server.RemoveSpawnedObject(Parent, exploded);
        }
    }
}
