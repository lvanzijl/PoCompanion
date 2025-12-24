using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEffortEstimationSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EffortEstimationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DefaultEffortTask = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortBug = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortUserStory = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortPBI = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortFeature = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortEpic = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultEffortGeneric = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableProactiveNotifications = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffortEstimationSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EffortEstimationSettings");
        }
    }
}
