using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForecastProjectionAsOfUtc",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ForecastProjections",
                columns: table => new
                {
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SprintsRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCompletionDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Confidence = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ProjectionVariantsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastProjections", x => x.WorkItemId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForecastProjections_WorkItemType",
                table: "ForecastProjections",
                column: "WorkItemType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForecastProjections");

            migrationBuilder.DropColumn(
                name: "ForecastProjectionAsOfUtc",
                table: "ProductOwnerCacheStates");
        }
    }
}
