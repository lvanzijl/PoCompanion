using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUtcDateColumnsForSqliteTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TfsChangedDateUtc",
                table: "WorkItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "TfsConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDateUtc",
                table: "Sprints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedDateUtc",
                table: "Sprints",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateUtc",
                table: "Sprints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateUtc",
                table: "PullRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateUtc",
                table: "PullRequestComments",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE WorkItems
                SET TfsChangedDateUtc = COALESCE(datetime(TfsChangedDate), '0001-01-01 00:00:00');
                """);

            migrationBuilder.Sql("""
                UPDATE TfsConfigs
                SET UpdatedAtUtc = COALESCE(datetime(UpdatedAt), '0001-01-01 00:00:00');
                """);

            migrationBuilder.Sql("""
                UPDATE Sprints
                SET StartDateUtc = CASE WHEN StartUtc IS NULL THEN NULL ELSE datetime(StartUtc) END,
                    EndDateUtc = CASE WHEN EndUtc IS NULL THEN NULL ELSE datetime(EndUtc) END,
                    LastSyncedDateUtc = COALESCE(datetime(LastSyncedUtc), '0001-01-01 00:00:00');
                """);

            migrationBuilder.Sql("""
                UPDATE PullRequests
                SET CreatedDateUtc = COALESCE(datetime(CreatedDate), '0001-01-01 00:00:00');
                """);

            migrationBuilder.Sql("""
                UPDATE PullRequestComments
                SET CreatedDateUtc = COALESCE(datetime(CreatedDate), '0001-01-01 00:00:00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_TfsChangedDateUtc",
                table: "WorkItems",
                column: "TfsChangedDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TfsConfigs_UpdatedAtUtc",
                table: "TfsConfigs",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_TeamId_LastSyncedDateUtc",
                table: "Sprints",
                columns: new[] { "TeamId", "LastSyncedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_TeamId_StartDateUtc",
                table: "Sprints",
                columns: new[] { "TeamId", "StartDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_CreatedDateUtc",
                table: "PullRequests",
                column: "CreatedDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_CreatedDateUtc",
                table: "PullRequestComments",
                column: "CreatedDateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_TfsChangedDateUtc",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_TfsConfigs_UpdatedAtUtc",
                table: "TfsConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_TeamId_LastSyncedDateUtc",
                table: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_Sprints_TeamId_StartDateUtc",
                table: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_PullRequests_CreatedDateUtc",
                table: "PullRequests");

            migrationBuilder.DropIndex(
                name: "IX_PullRequestComments_CreatedDateUtc",
                table: "PullRequestComments");

            migrationBuilder.DropColumn(
                name: "TfsChangedDateUtc",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "EndDateUtc",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "LastSyncedDateUtc",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "StartDateUtc",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CreatedDateUtc",
                table: "PullRequests");

            migrationBuilder.DropColumn(
                name: "CreatedDateUtc",
                table: "PullRequestComments");
        }
    }
}
