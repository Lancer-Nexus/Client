using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreLancer.Database.Migrations;

public partial class TransferSnapshotImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TransferSnapshotImports",
            columns: table => new
            {
                TransferId = table.Column<Guid>(type: "TEXT", nullable: false),
                AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                SourceInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                TargetInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                TargetSystemId = table.Column<string>(type: "TEXT", nullable: false),
                LeaseVersion = table.Column<long>(type: "INTEGER", nullable: false),
                TicketHash = table.Column<string>(type: "TEXT", nullable: false),
                SnapshotHash = table.Column<string>(type: "TEXT", nullable: true),
                Imported = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TransferSnapshotImports", x => x.TransferId));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "TransferSnapshotImports");
}
