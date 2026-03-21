using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildQualityDataFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Coverages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildId = table.Column<int>(type: "INTEGER", nullable: false),
                    CoveredLines = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLines = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CachedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coverages_CachedPipelineRuns_BuildId",
                        column: x => x.BuildId,
                        principalTable: "CachedPipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BuildId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalTests = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedTests = table.Column<int>(type: "INTEGER", nullable: false),
                    NotApplicableTests = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CachedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestRuns_CachedPipelineRuns_BuildId",
                        column: x => x.BuildId,
                        principalTable: "CachedPipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Coverages_BuildId",
                table: "Coverages",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Coverages_Timestamp",
                table: "Coverages",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_BuildId",
                table: "TestRuns",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_BuildId_ExternalId",
                table: "TestRuns",
                columns: new[] { "BuildId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Timestamp",
                table: "TestRuns",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coverages");

            migrationBuilder.DropTable(
                name: "TestRuns");
        }
    }
}
