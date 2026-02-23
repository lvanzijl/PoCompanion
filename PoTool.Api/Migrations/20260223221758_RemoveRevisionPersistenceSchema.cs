using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRevisionPersistenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevisionFieldDeltas");

            migrationBuilder.DropTable(
                name: "RevisionIngestionWatermarks");

            migrationBuilder.DropTable(
                name: "RevisionRelationDeltas");

            migrationBuilder.DropTable(
                name: "RevisionHeaders");

            migrationBuilder.DropColumn(
                name: "RevisionSource",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "RevisionSourceOverride",
                table: "Profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevisionSource",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RevisionSourceOverride",
                table: "Profiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RevisionHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AreaPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    BusinessValue = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ChangedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Effort = table.Column<double>(type: "REAL", nullable: true),
                    IngestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IterationPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
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
                    FallbackResumeIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    FallbackUsedLastRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInitialBackfillComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastErrorAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastIngestionCompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastIngestionRevisionCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastIngestionStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastRunOutcome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastStableChangedDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastStableContinuationTokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastSyncStartDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
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
                name: "RevisionFieldDeltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevisionHeaderId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
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
        }
    }
}
