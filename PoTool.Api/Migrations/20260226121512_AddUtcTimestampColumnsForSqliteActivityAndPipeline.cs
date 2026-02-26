using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUtcTimestampColumnsForSqliteActivityAndPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CachedPipelineRuns_FinishedDate",
                table: "CachedPipelineRuns");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEventLedgerEntries_ProductOwnerId_EventTimestamp",
                table: "ActivityEventLedgerEntries");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedDateUtc",
                table: "CachedPipelineRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EventTimestampUtc",
                table: "ActivityEventLedgerEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE CachedPipelineRuns
                SET FinishedDateUtc = CASE
                    WHEN FinishedDate IS NULL THEN NULL
                    ELSE datetime(FinishedDate)
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE ActivityEventLedgerEntries
                SET EventTimestampUtc = COALESCE(datetime(EventTimestamp), '0001-01-01 00:00:00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_FinishedDateUtc",
                table: "CachedPipelineRuns",
                column: "FinishedDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEventLedgerEntries_ProductOwnerId_EventTimestampUtc",
                table: "ActivityEventLedgerEntries",
                columns: new[] { "ProductOwnerId", "EventTimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CachedPipelineRuns_FinishedDateUtc",
                table: "CachedPipelineRuns");

            migrationBuilder.DropIndex(
                name: "IX_ActivityEventLedgerEntries_ProductOwnerId_EventTimestampUtc",
                table: "ActivityEventLedgerEntries");

            migrationBuilder.DropColumn(
                name: "FinishedDateUtc",
                table: "CachedPipelineRuns");

            migrationBuilder.DropColumn(
                name: "EventTimestampUtc",
                table: "ActivityEventLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_FinishedDate",
                table: "CachedPipelineRuns",
                column: "FinishedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEventLedgerEntries_ProductOwnerId_EventTimestamp",
                table: "ActivityEventLedgerEntries",
                columns: new[] { "ProductOwnerId", "EventTimestamp" });
        }
    }
}
