using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingEntitySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingTfsConnections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingTfsConnections",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingTfsConnections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingTeamSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingTeamSources",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingTeamSources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingProjectSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingProjectSources",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingProjectSources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingProductSourceBindings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingProductSourceBindings",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingProductSourceBindings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingProductRoots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingProductRoots",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingProductRoots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "OnboardingPipelineSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "OnboardingPipelineSources",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "OnboardingPipelineSources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTfsConnections_IsDeleted",
                table: "OnboardingTfsConnections",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTeamSources_IsDeleted",
                table: "OnboardingTeamSources",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProjectSources_IsDeleted",
                table: "OnboardingProjectSources",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductSourceBindings_IsDeleted",
                table: "OnboardingProductSourceBindings",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProductRoots_IsDeleted",
                table: "OnboardingProductRoots",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingPipelineSources_IsDeleted",
                table: "OnboardingPipelineSources",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OnboardingTfsConnections_IsDeleted",
                table: "OnboardingTfsConnections");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingTeamSources_IsDeleted",
                table: "OnboardingTeamSources");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProjectSources_IsDeleted",
                table: "OnboardingProjectSources");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProductSourceBindings_IsDeleted",
                table: "OnboardingProductSourceBindings");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProductRoots_IsDeleted",
                table: "OnboardingProductRoots");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingPipelineSources_IsDeleted",
                table: "OnboardingPipelineSources");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingTfsConnections");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingTfsConnections");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingTfsConnections");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingTeamSources");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingTeamSources");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingTeamSources");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingProjectSources");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingProjectSources");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingProjectSources");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingProductSourceBindings");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingProductSourceBindings");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingProductSourceBindings");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingProductRoots");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingProductRoots");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingProductRoots");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "OnboardingPipelineSources");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "OnboardingPipelineSources");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "OnboardingPipelineSources");
        }
    }
}
