using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEffortSettingsLastModifiedUtcForSqliteOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "EffortEstimationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE EffortEstimationSettings
                SET LastModifiedUtc = COALESCE(datetime(LastModified), '0001-01-01 00:00:00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EffortEstimationSettings_LastModifiedUtc",
                table: "EffortEstimationSettings",
                column: "LastModifiedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EffortEstimationSettings_LastModifiedUtc",
                table: "EffortEstimationSettings");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "EffortEstimationSettings");
        }
    }
}
