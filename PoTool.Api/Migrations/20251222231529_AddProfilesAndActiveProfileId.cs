using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilesAndActiveProfileId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveProfileId",
                table: "Settings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AreaPaths = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GoalIds = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Name",
                table: "Profiles",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropColumn(
                name: "ActiveProfileId",
                table: "Settings");
        }
    }
}
