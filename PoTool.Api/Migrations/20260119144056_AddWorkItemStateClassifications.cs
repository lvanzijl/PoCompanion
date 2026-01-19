using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStateClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemStateClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TfsProjectName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    WorkItemType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StateName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Classification = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemStateClassifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateClassifications_TfsProjectName_WorkItemType_StateName",
                table: "WorkItemStateClassifications",
                columns: new[] { "TfsProjectName", "WorkItemType", "StateName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemStateClassifications");
        }
    }
}
