using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityEventLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivityEventWatermark",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityEventLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdateId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldRefName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    EventTimestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IterationPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    FeatureId = table.Column<int>(type: "INTEGER", nullable: true),
                    EpicId = table.Column<int>(type: "INTEGER", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEventLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEventLedgerEntries_ProductOwnerId_EventTimestamp",
                table: "ActivityEventLedgerEntries",
                columns: new[] { "ProductOwnerId", "EventTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEventLedgerEntries_WorkItemId_UpdateId",
                table: "ActivityEventLedgerEntries",
                columns: new[] { "WorkItemId", "UpdateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEventLedgerEntries_WorkItemId_UpdateId_FieldRefName",
                table: "ActivityEventLedgerEntries",
                columns: new[] { "WorkItemId", "UpdateId", "FieldRefName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEventLedgerEntries");

            migrationBuilder.DropColumn(
                name: "ActivityEventWatermark",
                table: "ProductOwnerCacheStates");
        }
    }
}
