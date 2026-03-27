using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class CdcCriticalFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM PortfolioSnapshotItems
                WHERE SnapshotId IN (
                    SELECT SnapshotId
                    FROM (
                        SELECT SnapshotId,
                               ROW_NUMBER() OVER (
                                   PARTITION BY ProductId, TimestampUtc, Source
                                   ORDER BY SnapshotId DESC
                               ) AS DuplicateRank
                        FROM PortfolioSnapshots
                    ) AS DuplicateSnapshots
                    WHERE DuplicateRank > 1
                );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM PortfolioSnapshots
                WHERE SnapshotId IN (
                    SELECT SnapshotId
                    FROM (
                        SELECT SnapshotId,
                               ROW_NUMBER() OVER (
                                   PARTITION BY ProductId, TimestampUtc, Source
                                   ORDER BY SnapshotId DESC
                               ) AS DuplicateRank
                        FROM PortfolioSnapshots
                    ) AS DuplicateSnapshots
                    WHERE DuplicateRank > 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_PortfolioSnapshots_ProductId_TimestampUtc",
                table: "PortfolioSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_ProductId_TimestampUtc_Source",
                table: "PortfolioSnapshots",
                columns: new[] { "ProductId", "TimestampUtc", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioSnapshots_ProductId_TimestampUtc_Source",
                table: "PortfolioSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_ProductId_TimestampUtc",
                table: "PortfolioSnapshots",
                columns: new[] { "ProductId", "TimestampUtc" });
        }
    }
}
