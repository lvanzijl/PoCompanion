using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPullRequestWorkItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PullRequestWorkItemLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestWorkItemLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestWorkItemLinks_PullRequestId",
                table: "PullRequestWorkItemLinks",
                column: "PullRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestWorkItemLinks_PullRequestId_WorkItemId",
                table: "PullRequestWorkItemLinks",
                columns: new[] { "PullRequestId", "WorkItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequestWorkItemLinks");
        }
    }
}
