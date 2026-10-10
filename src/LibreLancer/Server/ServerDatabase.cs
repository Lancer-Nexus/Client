// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using LibreLancer.Database;
using LibreLancer.Entities.Character;
using LibreLancer.Entities.Enums;
using LibreLancer.Data.Schema.Save;
using LibreLancer.Net.Protocol;
using Microsoft.EntityFrameworkCore;

namespace LibreLancer.Server
{

    public sealed record TransferSnapshotImportRecord(
        Guid TransferId, Guid AccountId, long CharacterId, string SourceInstanceId,
        string TargetInstanceId, string TargetSystemId, long LeaseVersion, string TicketHash,
        string? SnapshotHash, bool Imported);

    public class DatabaseCharacter
    {
        public long Id;
        private ServerDatabase db;
        private Character? cached;

        internal DatabaseCharacter(Character c, ServerDatabase db)
        {
            Id = c.Id;
            cached = c;
            this.db = db;
        }

        public async Task Update(Action<Character> update, bool updatingCargo)
        {
            await db.Run(async () =>
            {
                await using var ctx = db.CreateDbContext();
                Character self;
                if (updatingCargo)
                {
                    self = await ctx.Characters
                        .Include(c => c.Items)
                        .AsSplitQuery()
                        .FirstAsync(c => c.Id == Id);
                }
                else
                {
                    self = await ctx.Characters
                        .FirstAsync(c => c.Id == Id);
                }
                update(self);
                cached = self;
                await ctx.SaveChangesAsync();
            });
        }

        public async Task UpdateFactionReps(IEnumerable<KeyValuePair<string, float>> reps)
        {
            await db.Run(async () =>
            {
                await using var ctx = db.CreateDbContext();
                await ctx.UpsertRepValues(Id, reps);
            });
        }

        public async Task UpdateVisitFlags(IEnumerable<KeyValuePair<uint, Visit>> flags)
        {
            await db.Run(async () =>
            {
                await using var ctx = db.CreateDbContext();
                await ctx.UpsertVisitValues(Id, flags);
            });
        }

        public async Task AddVisitHistory(IEnumerable<VisitHistoryInput> history)
        {
            await db.Run(async () =>
            {
                await using var ctx = db.CreateDbContext();
                await ctx.InsertVisitHistoryNonConflicting(Id, history);
            });
        }

        public async Task<Character> GetCharacter()
        {
            return await db.Run(async () =>
            {
                if (cached != null)
                    return cached;
                await using var ctx = db.CreateDbContext();
                cached = ctx.Characters
                    .Include(c => c.Items)
                    .Include(c => c.Reputations)
                    .Include(c => c.VisitEntries)
                    .First(c => c.Id == Id);
                return cached;
            });
        }
    }

    public record BannedPlayerDescription(Guid? AccountId, string[] Characters, DateTime? BanExpiry);


    public class ServerDatabase : IDisposable
    {
        private GameServer server;
        private BufferBlock<Func<Task>> actions = new();
        private Task actionQueueTask;

		public ServerDatabase(GameServer server)
		{
		    this.server = server;
            actionQueueTask = Task.Run(ProcessTaskQueue);
		}

        // RunContinuationsAsynchronously seems to avoid deadlocks
        internal Task<T> Run<T>(Func<Task<T>> func)
        {
            var compSrc = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            actions.Post(async () =>
            {
                try
                {
                    var result = await func();
                    compSrc.SetResult(result);
                }
                catch (Exception e)
                {
                    compSrc.SetException(e);
                }
            });
            return compSrc.Task;
        }

        internal Task Run(Func<Task> func)
        {
            var compSrc = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            actions.Post(async () =>
            {
                try
                {
                    await func();
                    compSrc.SetResult();
                }
                catch (Exception e)
                {
                    compSrc.SetException(e);
                }
            });
            return compSrc.Task;
        }

        private async Task ProcessTaskQueue()
        {
            while (await actions.OutputAvailableAsync())
            {
                var item = await actions.ReceiveAsync();
                await item().ConfigureAwait(false);
            }
        }

        public LibreLancerContext CreateDbContext() => server.DbContextFactory.CreateDbContext([]);

        public async Task BanAccount(Guid playerGuid, DateTime expiryUtc)
        {
            await using var ctx = CreateDbContext();
            var acc = ctx.Accounts.FirstOrDefault(x => x.AccountIdentifier == playerGuid);
            if(acc != null)
                acc.BanExpiry = expiryUtc;
            await ctx.SaveChangesAsync();

            server.ServerEvents.Enqueue(new ServerEvent
            {
                Type = ServerEventType.PlayerBanChanged,
                TimeUtc = DateTime.UtcNow,
                Payload = new PlayerBanChangedEventPayload(
                    new BannedPlayerDescription(acc!.AccountIdentifier, acc.Characters.Select(c=>c.Name).ToArray(), expiryUtc),
                    true)
            });
        }

        public async Task UnbanAccount(Guid playerGuid)
        {
            await using var ctx = CreateDbContext();
            var acc = ctx.Accounts.FirstOrDefault(x => x.AccountIdentifier == playerGuid);
            if (acc != null)
                acc.BanExpiry = null;
            await ctx.SaveChangesAsync();

            server.ServerEvents.Enqueue(new ServerEvent
            {
                Type = ServerEventType.PlayerBanChanged,
                TimeUtc = DateTime.UtcNow,
                Payload = new PlayerBanChangedEventPayload(
                    new BannedPlayerDescription(acc!.AccountIdentifier, acc.Characters.Select(c => c.Name).ToArray(), null),
                    false)
            });
        }

        public BannedPlayerDescription[] GetBannedPlayers()
        {
            using var ctx = CreateDbContext();
            return ctx.Accounts.Where(x => x.BanExpiry != null)
                .Select(x => new BannedPlayerDescription(
                    x.AccountIdentifier,
                    x.Characters.Select(y => y.Name).ToArray(),
                    x.BanExpiry)).ToArray();
        }

        public async Task<long?> FindCharacter(string character)
        {
            return await Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var c = ctx.Characters.Select(x => new {x.Id, x.Name}).FirstOrDefault(c => c.Name == character);
                return c?.Id;
            });
        }

        public async Task<Guid?> FindAccount(string character)
        {
            return await Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var c = ctx.Characters.Include(x => x.Account).FirstOrDefault(x => x.Name == character);
                c ??= ctx.Characters.Include(x => x.Account).FirstOrDefault(x => x.Name.Contains(character));
                return c?.Account?.AccountIdentifier;
            });
        }

        public async Task<List<SelectableCharacter>?> PlayerLogin(Guid playerGuid)
        {
            return await Run(async () =>
            {
                await using var ctx = CreateDbContext();
                ctx.ChangeTracker.AutoDetectChangesEnabled = false;
                var acc = ctx.Accounts.Where(x => x.AccountIdentifier == playerGuid)
                    .Include(x => x.Characters)
                    .FirstOrDefault();

                if (acc == null)
                {
                    var utcNow = DateTime.UtcNow;
                    acc = new Account()
                    {
                        AccountIdentifier = playerGuid,
                        LastLogin = utcNow,
                        CreationDate = utcNow
                    };
                    ctx.Accounts.Add(acc);
                    ctx.SaveChanges();
                    return [];
                }

                if (acc.BanExpiry.HasValue && acc.BanExpiry > DateTime.UtcNow)
                {
                    return null;
                }

                ctx.Entry(acc).Property(x => x.LastLogin).CurrentValue = DateTime.UtcNow;
                ctx.SaveChanges();

                return acc.Characters.Select(c => new SelectableCharacter()
                    {
                        Location = c.System,
                        Funds = c.Money,
                        Name = c.Name,
                        Rank = (int) c.Rank,
                        Ship = c.Ship,
                        Id = c.Id
                    })
                    .ToList();
            });

        }

        public void DeleteCharacter(long characterId)
        {
            Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var ch = ctx.Characters.First(x => x.Id == characterId);
                ctx.Characters.Remove(ch);
                await ctx.SaveChangesAsync();
            });
        }

        public bool NameInUse(string name)
        {
            using var ctx = CreateDbContext();
            return ctx.Characters.Any(x => x.Name.Equals(name));
        }

        public async Task<DatabaseCharacter> GetCharacter(long id)
        {
            return await Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var character = await ctx.Characters
                    .Include(c => c.Items)
                    .Include(c => c.Reputations)
                    .Include(c => c.VisitEntries)
                    .AsSplitQuery()
                    .FirstAsync(c => c.Id == id);
                return new DatabaseCharacter(character, this);
            });
        }

        public async Task<long> AddCharacter(Guid playerGuid, Action<Character> fillCharacter)
        {
            return await Run(async () =>
            {
                await using var ctx = CreateDbContext();
                // Get account
                var acc = ctx.Accounts.First(x => x.AccountIdentifier == playerGuid);

                // Init object
                var c = new Character
                {
                    Account = acc,
                    UpdateDate =  DateTime.UtcNow,
                    CreationDate =  DateTime.UtcNow,
                };

                fillCharacter(c);

                // Add
                ctx.Characters.Add(c);
                await ctx.SaveChangesAsync();
                return c.Id;
            });
        }

        public Task<TransferSnapshotImportRecord?> GetTransferSnapshotImportAsync(Guid transferId) =>
            Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var record = await ctx.TransferSnapshotImports.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TransferId == transferId);
                return record == null ? null : ToImportRecord(record);
            });

        public Task RecordTransferSnapshotImportAsync(
            Guid transferId, Guid accountId, long characterId, string sourceInstanceId,
            string targetInstanceId, string targetSystemId, long leaseVersion, string ticketHash)
        {
            return Run(async () =>
            {
                await using var ctx = CreateDbContext();
                var existing = await ctx.TransferSnapshotImports
                    .FirstOrDefaultAsync(x => x.TransferId == transferId);
                if (existing != null)
                {
                    if (existing.AccountId != accountId || existing.CharacterId != characterId ||
                        existing.LeaseVersion != leaseVersion ||
                        !string.Equals(existing.SourceInstanceId, sourceInstanceId, StringComparison.Ordinal) ||
                        !string.Equals(existing.TargetInstanceId, targetInstanceId, StringComparison.Ordinal) ||
                        !string.Equals(existing.TargetSystemId, targetSystemId, StringComparison.Ordinal) ||
                        !string.Equals(existing.TicketHash, ticketHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("Transfer ID is already bound to different import claims.");
                    return;
                }

                ctx.TransferSnapshotImports.Add(new TransferSnapshotImport
                {
                    TransferId = transferId,
                    AccountId = accountId,
                    CharacterId = characterId,
                    SourceInstanceId = sourceInstanceId,
                    TargetInstanceId = targetInstanceId,
                    TargetSystemId = targetSystemId,
                    LeaseVersion = leaseVersion,
                    TicketHash = ticketHash
                });
                await ctx.SaveChangesAsync();
            });
        }

        public Task<bool> ApplyTransferSnapshotImportAsync(Guid transferId, string snapshotHash, SaveGame save)
        {
            return Run(async () =>
            {
                await using var ctx = CreateDbContext();
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                var import = await ctx.TransferSnapshotImports
                    .FirstOrDefaultAsync(x => x.TransferId == transferId)
                    ?? throw new InvalidOperationException("Transfer import claims have not been recorded.");
                if (import.Imported)
                {
                    if (!string.Equals(import.SnapshotHash, snapshotHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("Transfer snapshot changed after it was imported.");
                    await transaction.CommitAsync();
                    return false;
                }

                if (save.Player == null || string.IsNullOrWhiteSpace(save.Player.Name) ||
                    !string.Equals(save.Player.System, import.TargetSystemId, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(save.Player.Base))
                    throw new InvalidDataException("Transfer snapshot character is not valid for the target system.");

                var account = await ctx.Accounts.FirstOrDefaultAsync(x => x.AccountIdentifier == import.AccountId);
                if (account == null)
                {
                    account = new Account { AccountIdentifier = import.AccountId, LastLogin = DateTime.UtcNow };
                    ctx.Accounts.Add(account);
                }

                var character = await ctx.Characters
                    .Include(x => x.Account)
                    .Include(x => x.Items)
                    .Include(x => x.Reputations)
                    .Include(x => x.VisitEntries)
                    .Include(x => x.VisitHistoryEntries)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == import.CharacterId);
                if (character == null)
                {
                    character = new Character { Id = import.CharacterId, Account = account };
                    ctx.Characters.Add(character);
                }
                else if (character.Account.AccountIdentifier != import.AccountId ||
                         !string.Equals(character.Name, save.Player.Name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Target character identity does not match the transfer.");
                }

                NetCharacter.SaveToDbCharacter(server, save, character);
                import.SnapshotHash = snapshotHash;
                import.Imported = true;
                await ctx.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            });
        }

        private static TransferSnapshotImportRecord ToImportRecord(TransferSnapshotImport record) => new(
            record.TransferId, record.AccountId, record.CharacterId, record.SourceInstanceId,
            record.TargetInstanceId, record.TargetSystemId, record.LeaseVersion, record.TicketHash,
            record.SnapshotHash, record.Imported);

        public void Dispose()
        {
            actions.Complete();
            actionQueueTask.Wait();
        }
    }
}
