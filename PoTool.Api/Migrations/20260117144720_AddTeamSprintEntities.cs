using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSprintEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedIterationsUtc",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Teams",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TfsTeamId",
                table: "Teams",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TfsTeamName",
                table: "Teams",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PipelineDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PipelineDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    RepoId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RepoName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    YamlPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LastSyncedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineDefinitions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PipelineDefinitions_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    TfsIterationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TimeFrame = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastSyncedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineDefinitions_ProductId_PipelineDefinitionId",
                table: "PipelineDefinitions",
                columns: new[] { "ProductId", "PipelineDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineDefinitions_RepositoryId",
                table: "PipelineDefinitions",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_TeamId_Path",
                table: "Sprints",
                columns: new[] { "TeamId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineDefinitions");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropColumn(
                name: "LastSyncedIterationsUtc",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TfsTeamId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TfsTeamName",
                table: "Teams");
        }
    }
}
