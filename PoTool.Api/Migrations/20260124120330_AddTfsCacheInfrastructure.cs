using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTfsCacheInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TfsChangedDate",
                table: "WorkItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "TfsETag",
                table: "WorkItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TfsRevision",
                table: "WorkItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CachedMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MetricValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedMetrics_Profiles_ProductOwnerId",
                        column: x => x.ProductOwnerId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CachedPipelineRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PipelineDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TfsRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    RunName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinishedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SourceBranch = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SourceVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CachedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedPipelineRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CachedPipelineRuns_PipelineDefinitions_PipelineDefinitionId",
                        column: x => x.PipelineDefinitionId,
                        principalTable: "PipelineDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CachedPipelineRuns_Profiles_ProductOwnerId",
                        column: x => x.ProductOwnerId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductOwnerCacheStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptSync = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSuccessfulSync = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    WorkItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PullRequestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PipelineCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemWatermark = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PullRequestWatermark = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PipelineWatermark = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CurrentSyncStage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StageProgressPercent = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOwnerCacheStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductOwnerCacheStates_Profiles_ProductOwnerId",
                        column: x => x.ProductOwnerId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedMetrics_ProductOwnerId_MetricName",
                table: "CachedMetrics",
                columns: new[] { "ProductOwnerId", "MetricName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_FinishedDate",
                table: "CachedPipelineRuns",
                column: "FinishedDate");

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_PipelineDefinitionId",
                table: "CachedPipelineRuns",
                column: "PipelineDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_ProductOwnerId_PipelineDefinitionId_TfsRunId",
                table: "CachedPipelineRuns",
                columns: new[] { "ProductOwnerId", "PipelineDefinitionId", "TfsRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductOwnerCacheStates_ProductOwnerId",
                table: "ProductOwnerCacheStates",
                column: "ProductOwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedMetrics");

            migrationBuilder.DropTable(
                name: "CachedPipelineRuns");

            migrationBuilder.DropTable(
                name: "ProductOwnerCacheStates");

            migrationBuilder.DropColumn(
                name: "TfsChangedDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TfsETag",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TfsRevision",
                table: "WorkItems");
        }
    }
}
