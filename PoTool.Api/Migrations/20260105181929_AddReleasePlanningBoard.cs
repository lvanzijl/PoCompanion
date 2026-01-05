using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReleasePlanningBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedValidationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EpicId = table.Column<int>(type: "INTEGER", nullable: false),
                    Indicator = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedValidationResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IterationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VerticalPosition = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IterationLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lanes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ObjectiveId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lanes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MilestoneLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VerticalPosition = table.Column<double>(type: "REAL", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EpicPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EpicId = table.Column<int>(type: "INTEGER", nullable: false),
                    LaneId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderInRow = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpicPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpicPlacements_Lanes_LaneId",
                        column: x => x.LaneId,
                        principalTable: "Lanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedValidationResults_EpicId",
                table: "CachedValidationResults",
                column: "EpicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EpicPlacements_EpicId",
                table: "EpicPlacements",
                column: "EpicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EpicPlacements_LaneId_RowIndex_OrderInRow",
                table: "EpicPlacements",
                columns: new[] { "LaneId", "RowIndex", "OrderInRow" });

            migrationBuilder.CreateIndex(
                name: "IX_Lanes_ObjectiveId",
                table: "Lanes",
                column: "ObjectiveId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedValidationResults");

            migrationBuilder.DropTable(
                name: "EpicPlacements");

            migrationBuilder.DropTable(
                name: "IterationLines");

            migrationBuilder.DropTable(
                name: "MilestoneLines");

            migrationBuilder.DropTable(
                name: "Lanes");
        }
    }
}
