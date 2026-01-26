using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningBoardTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    RowType = table.Column<int>(type: "INTEGER", nullable: false),
                    MarkerRowType = table.Column<int>(type: "INTEGER", nullable: true),
                    MarkerLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardRows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanningBoardSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedProductId = table.Column<int>(type: "INTEGER", nullable: true),
                    HiddenProductIdsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningBoardSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningBoardSettings_Profiles_ProductOwnerId",
                        column: x => x.ProductOwnerId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningEpicPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EpicId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    RowId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderInCell = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningEpicPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningEpicPlacements_BoardRows_RowId",
                        column: x => x.RowId,
                        principalTable: "BoardRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningEpicPlacements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardRows_DisplayOrder",
                table: "BoardRows",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningBoardSettings_ProductOwnerId",
                table: "PlanningBoardSettings",
                column: "ProductOwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningEpicPlacements_EpicId",
                table: "PlanningEpicPlacements",
                column: "EpicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningEpicPlacements_ProductId_RowId_OrderInCell",
                table: "PlanningEpicPlacements",
                columns: new[] { "ProductId", "RowId", "OrderInCell" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningEpicPlacements_RowId",
                table: "PlanningEpicPlacements",
                column: "RowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningBoardSettings");

            migrationBuilder.DropTable(
                name: "PlanningEpicPlacements");

            migrationBuilder.DropTable(
                name: "BoardRows");
        }
    }
}
