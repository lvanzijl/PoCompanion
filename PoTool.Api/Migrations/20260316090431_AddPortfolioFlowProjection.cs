using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioFlowProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioFlowProjections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SprintId = table.Column<int>(type: "INTEGER", nullable: false),
                    StockStoryPoints = table.Column<double>(type: "REAL", nullable: false),
                    RemainingScopeStoryPoints = table.Column<double>(type: "REAL", nullable: false),
                    InflowStoryPoints = table.Column<double>(type: "REAL", nullable: false),
                    ThroughputStoryPoints = table.Column<double>(type: "REAL", nullable: false),
                    CompletionPercent = table.Column<double>(type: "REAL", nullable: true),
                    ProjectionTimestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioFlowProjections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioFlowProjections_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortfolioFlowProjections_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioFlowProjections_ProductId",
                table: "PortfolioFlowProjections",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioFlowProjections_SprintId",
                table: "PortfolioFlowProjections",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioFlowProjections_SprintId_ProductId",
                table: "PortfolioFlowProjections",
                columns: new[] { "SprintId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioFlowProjections");
        }
    }
}
