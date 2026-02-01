using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTriageTablesAndBugTriageState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BugTriageStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BugId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FirstObservedCriticality = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsTriaged = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastTriageActionAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BugTriageStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriageTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageTags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BugTriageStates_BugId",
                table: "BugTriageStates",
                column: "BugId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageTags_DisplayOrder",
                table: "TriageTags",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_TriageTags_Name",
                table: "TriageTags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BugTriageStates");

            migrationBuilder.DropTable(
                name: "TriageTags");
        }
    }
}
