using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingMigrationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingMigrationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    MigrationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EnvironmentRing = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TriggerType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExecutionMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TotalUnitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SucceededUnitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedUnitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedUnitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SucceededEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockingIssueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMigrationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingMigrationUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnitIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    MigrationRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExecutionOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProcessedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SucceededEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedEntityCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMigrationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingMigrationUnits_OnboardingMigrationRuns_MigrationRunId",
                        column: x => x.MigrationRunId,
                        principalTable: "OnboardingMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingMigrationIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IssueIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    MigrationRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    MigrationUnitId = table.Column<int>(type: "INTEGER", nullable: true),
                    IssueType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IssueCategory = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceLegacyReference = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    TargetEntityType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TargetExternalIdentity = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SanitizedMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SanitizedDetails = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    IsBlocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMigrationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingMigrationIssues_OnboardingMigrationRuns_MigrationRunId",
                        column: x => x.MigrationRunId,
                        principalTable: "OnboardingMigrationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingMigrationIssues_OnboardingMigrationUnits_MigrationUnitId",
                        column: x => x.MigrationUnitId,
                        principalTable: "OnboardingMigrationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationIssues_IssueIdentifier",
                table: "OnboardingMigrationIssues",
                column: "IssueIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationIssues_MigrationRunId_Severity_IssueCategory",
                table: "OnboardingMigrationIssues",
                columns: new[] { "MigrationRunId", "Severity", "IssueCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationIssues_MigrationUnitId",
                table: "OnboardingMigrationIssues",
                column: "MigrationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationRuns_MigrationVersion_EnvironmentRing_ExecutionMode",
                table: "OnboardingMigrationRuns",
                columns: new[] { "MigrationVersion", "EnvironmentRing", "ExecutionMode" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationRuns_RunIdentifier",
                table: "OnboardingMigrationRuns",
                column: "RunIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationUnits_MigrationRunId_ExecutionOrder",
                table: "OnboardingMigrationUnits",
                columns: new[] { "MigrationRunId", "ExecutionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMigrationUnits_UnitIdentifier",
                table: "OnboardingMigrationUnits",
                column: "UnitIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingMigrationIssues");

            migrationBuilder.DropTable(
                name: "OnboardingMigrationUnits");

            migrationBuilder.DropTable(
                name: "OnboardingMigrationRuns");
        }
    }
}
