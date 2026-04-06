using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingPersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingTfsConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectionKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OrganizationUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AuthenticationMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AvailabilityValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AvailabilityValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AvailabilityValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AvailabilityValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    AvailabilityValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    PermissionValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PermissionValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PermissionValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PermissionValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    PermissionValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapabilityValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapabilityValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CapabilityValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CapabilityValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CapabilityValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSuccessfulValidationAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAttemptedValidationAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationFailureReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    LastVerifiedCapabilitiesSummary = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTfsConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProjectSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TfsConnectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_ProjectExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Snapshot_Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Snapshot_Metadata_ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_RenameDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_StaleReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProjectSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingProjectSources_OnboardingTfsConnections_TfsConnectionId",
                        column: x => x.TfsConnectionId,
                        principalTable: "OnboardingTfsConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingPipelineSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PipelineExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_PipelineExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_ProjectExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Snapshot_Folder = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Snapshot_YamlPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Snapshot_RepositoryExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Snapshot_RepositoryName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Snapshot_Metadata_ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_RenameDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_StaleReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingPipelineSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingPipelineSources_OnboardingProjectSources_ProjectSourceId",
                        column: x => x.ProjectSourceId,
                        principalTable: "OnboardingProjectSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProductRoots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_WorkItemExternalId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Snapshot_Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Snapshot_WorkItemType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_State = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_ProjectExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_AreaPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Snapshot_Metadata_ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_RenameDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_StaleReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProductRoots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingProductRoots_OnboardingProjectSources_ProjectSourceId",
                        column: x => x.ProjectSourceId,
                        principalTable: "OnboardingProjectSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingTeamSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_TeamExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_ProjectExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Snapshot_Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Snapshot_DefaultAreaPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Snapshot_Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Snapshot_Metadata_ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Snapshot_Metadata_IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_RenameDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    Snapshot_Metadata_StaleReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingTeamSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingTeamSources_OnboardingProjectSources_ProjectSourceId",
                        column: x => x.ProjectSourceId,
                        principalTable: "OnboardingProjectSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProductSourceBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductRootId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamSourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    PipelineSourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceExternalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidationState_Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationState_ValidatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidationState_ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ValidationState_Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ValidationState_IsRetryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProductSourceBindings", x => x.Id);
                    table.CheckConstraint("CK_OnboardingProductSourceBindings_SourceReference", "(\"SourceType\" = 'Project' AND \"TeamSourceId\" IS NULL AND \"PipelineSourceId\" IS NULL) OR\n(\"SourceType\" = 'Team' AND \"TeamSourceId\" IS NOT NULL AND \"PipelineSourceId\" IS NULL) OR\n(\"SourceType\" = 'Pipeline' AND \"PipelineSourceId\" IS NOT NULL AND \"TeamSourceId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_OnboardingProductSourceBindings_OnboardingPipelineSources_PipelineSourceId",
                        column: x => x.PipelineSourceId,
                        principalTable: "OnboardingPipelineSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingProductSourceBindings_OnboardingProductRoots_ProductRootId",
                        column: x => x.ProductRootId,
                        principalTable: "OnboardingProductRoots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingProductSourceBindings_OnboardingProjectSources_ProjectSourceId",
                        column: x => x.ProjectSourceId,
                        principalTable: "OnboardingProjectSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingProductSourceBindings_OnboardingTeamSources_TeamSourceId",
                        column: x => x.TeamSourceId,
                        principalTable: "OnboardingTeamSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingPipelineSources_ProjectSourceId_PipelineExternalId",
                table: "OnboardingPipelineSources",
                columns: new[] { "ProjectSourceId", "PipelineExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductRoots_ProjectSourceId_WorkItemExternalId",
                table: "OnboardingProductRoots",
                columns: new[] { "ProjectSourceId", "WorkItemExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductSourceBindings_PipelineSourceId",
                table: "OnboardingProductSourceBindings",
                column: "PipelineSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductSourceBindings_ProductRootId_SourceType_SourceExternalId",
                table: "OnboardingProductSourceBindings",
                columns: new[] { "ProductRootId", "SourceType", "SourceExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductSourceBindings_ProjectSourceId",
                table: "OnboardingProductSourceBindings",
                column: "ProjectSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductSourceBindings_TeamSourceId",
                table: "OnboardingProductSourceBindings",
                column: "TeamSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProjectSources_TfsConnectionId_ProjectExternalId",
                table: "OnboardingProjectSources",
                columns: new[] { "TfsConnectionId", "ProjectExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTeamSources_ProjectSourceId_TeamExternalId",
                table: "OnboardingTeamSources",
                columns: new[] { "ProjectSourceId", "TeamExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTfsConnections_ConnectionKey",
                table: "OnboardingTfsConnections",
                column: "ConnectionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTfsConnections_OrganizationUrl",
                table: "OnboardingTfsConnections",
                column: "OrganizationUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingProductSourceBindings");

            migrationBuilder.DropTable(
                name: "OnboardingPipelineSources");

            migrationBuilder.DropTable(
                name: "OnboardingProductRoots");

            migrationBuilder.DropTable(
                name: "OnboardingTeamSources");

            migrationBuilder.DropTable(
                name: "OnboardingProjectSources");

            migrationBuilder.DropTable(
                name: "OnboardingTfsConnections");
        }
    }
}
