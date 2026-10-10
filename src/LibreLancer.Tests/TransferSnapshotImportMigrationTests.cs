using System;
using System.Threading.Tasks;
using LibreLancer.Database;
using LibreLancer.Entities.Character;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibreLancer.Tests;

public class TransferSnapshotImportMigrationTests
{
    [Fact]
    public async Task MigrationCreatesDurableImportReceiptTable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<LibreLancerContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new LibreLancerContext(options);

        await context.Database.MigrateAsync(cancellationToken);

        Assert.Contains("20260926120000_TransferSnapshotImports",
            await context.Database.GetAppliedMigrationsAsync(cancellationToken));

        var transferId = Guid.NewGuid();
        context.TransferSnapshotImports.Add(new TransferSnapshotImport
        {
            TransferId = transferId,
            AccountId = Guid.NewGuid(),
            CharacterId = 42,
            SourceInstanceId = "li01-instance",
            TargetInstanceId = "li02-instance",
            TargetSystemId = "li02",
            LeaseVersion = 8,
            TicketHash = new string('A', 64),
            SnapshotHash = new string('B', 64),
            Imported = true
        });
        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.TransferSnapshotImports.SingleAsync(
            x => x.TransferId == transferId, cancellationToken);
        Assert.Equal(42, saved.CharacterId);
        Assert.True(saved.Imported);
    }
}
