using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequestsAndUpdateTfsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiVersion",
                table: "TfsConfigs",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AuthMode",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastValidated",
                table: "TfsConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseDefaultCredentials",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PullRequestComments",
                columns: table => new
                {
                    InternalId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolvedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResolvedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestComments", x => x.InternalId);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestFileChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    IterationId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LinesAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    LinesDeleted = table.Column<int>(type: "INTEGER", nullable: false),
                    LinesModified = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestFileChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestIterations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    IterationNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestIterations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PullRequests",
                columns: table => new
                {
                    InternalId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IterationPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceBranch = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetBranch = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequests", x => x.InternalId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_PullRequestId",
                table: "PullRequestComments",
                column: "PullRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestComments_ThreadId",
                table: "PullRequestComments",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestFileChanges_PullRequestId_IterationId",
                table: "PullRequestFileChanges",
                columns: new[] { "PullRequestId", "IterationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestIterations_PullRequestId_IterationNumber",
                table: "PullRequestIterations",
                columns: new[] { "PullRequestId", "IterationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_CreatedBy",
                table: "PullRequests",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_Id",
                table: "PullRequests",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_IterationPath",
                table: "PullRequests",
                column: "IterationPath");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_RepositoryName",
                table: "PullRequests",
                column: "RepositoryName");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_Status",
                table: "PullRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequestComments");

            migrationBuilder.DropTable(
                name: "PullRequestFileChanges");

            migrationBuilder.DropTable(
                name: "PullRequestIterations");

            migrationBuilder.DropTable(
                name: "PullRequests");

            migrationBuilder.DropColumn(
                name: "ApiVersion",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "AuthMode",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "LastValidated",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "UseDefaultCredentials",
                table: "TfsConfigs");
        }
    }
}
