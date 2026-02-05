using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionTrackingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResolvedWorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResolvedProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolvedEpicId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolvedFeatureId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolvedSprintId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtRevision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResolvedWorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevisionHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IterationPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AreaPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ChangedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Effort = table.Column<int>(type: "INTEGER", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IngestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevisionIngestionWatermarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContinuationToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastSyncStartDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastIngestionStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastIngestionCompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastIngestionRevisionCount = table.Column<int>(type: "INTEGER", nullable: true),
                    IsInitialBackfillComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastErrorAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionIngestionWatermarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionIngestionWatermarks_Profiles_ProductOwnerId",
                        column: x => x.ProductOwnerId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprintMetricsProjections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SprintId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedEffort = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkedEffort = table.Column<int>(type: "INTEGER", nullable: false),
                    BugsPlannedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BugsWorkedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IncludedUpToRevisionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintMetricsProjections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintMetricsProjections_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintMetricsProjections_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionFieldDeltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevisionHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionFieldDeltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionFieldDeltas_RevisionHeaders_RevisionHeaderId",
                        column: x => x.RevisionHeaderId,
                        principalTable: "RevisionHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionRelationDeltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevisionHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeType = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TargetWorkItemId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionRelationDeltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionRelationDeltas_RevisionHeaders_RevisionHeaderId",
                        column: x => x.RevisionHeaderId,
                        principalTable: "RevisionHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_ResolutionStatus",
                table: "ResolvedWorkItems",
                column: "ResolutionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_ResolvedEpicId",
                table: "ResolvedWorkItems",
                column: "ResolvedEpicId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_ResolvedFeatureId",
                table: "ResolvedWorkItems",
                column: "ResolvedFeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_ResolvedProductId",
                table: "ResolvedWorkItems",
                column: "ResolvedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_ResolvedSprintId",
                table: "ResolvedWorkItems",
                column: "ResolvedSprintId");

            migrationBuilder.CreateIndex(
                name: "IX_ResolvedWorkItems_WorkItemId",
                table: "ResolvedWorkItems",
                column: "WorkItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFieldDeltas_FieldName",
                table: "RevisionFieldDeltas",
                column: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFieldDeltas_RevisionHeaderId",
                table: "RevisionFieldDeltas",
                column: "RevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_ChangedDate",
                table: "RevisionHeaders",
                column: "ChangedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_IterationPath",
                table: "RevisionHeaders",
                column: "IterationPath");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_State",
                table: "RevisionHeaders",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_WorkItemId",
                table: "RevisionHeaders",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_WorkItemId_RevisionNumber",
                table: "RevisionHeaders",
                columns: new[] { "WorkItemId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_WorkItemType",
                table: "RevisionHeaders",
                column: "WorkItemType");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionIngestionWatermarks_ProductOwnerId",
                table: "RevisionIngestionWatermarks",
                column: "ProductOwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionRelationDeltas_RevisionHeaderId",
                table: "RevisionRelationDeltas",
                column: "RevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionRelationDeltas_TargetWorkItemId",
                table: "RevisionRelationDeltas",
                column: "TargetWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintMetricsProjections_ProductId",
                table: "SprintMetricsProjections",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintMetricsProjections_SprintId",
                table: "SprintMetricsProjections",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintMetricsProjections_SprintId_ProductId",
                table: "SprintMetricsProjections",
                columns: new[] { "SprintId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResolvedWorkItems");

            migrationBuilder.DropTable(
                name: "RevisionFieldDeltas");

            migrationBuilder.DropTable(
                name: "RevisionIngestionWatermarks");

            migrationBuilder.DropTable(
                name: "RevisionRelationDeltas");

            migrationBuilder.DropTable(
                name: "SprintMetricsProjections");

            migrationBuilder.DropTable(
                name: "RevisionHeaders");
        }
    }
}
