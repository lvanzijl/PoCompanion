using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshotItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkPackage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Progress = table.Column<double>(type: "REAL", nullable: false),
                    TotalWeight = table.Column<double>(type: "REAL", nullable: false),
                    LifecycleState = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshotItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshotItems_PortfolioSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "PortfolioSnapshots",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshotItems_SnapshotId",
                table: "PortfolioSnapshotItems",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_ProductId_IsArchived_TimestampUtc_SnapshotId",
                table: "PortfolioSnapshots",
                columns: new[] { "ProductId", "IsArchived", "TimestampUtc", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_ProductId_TimestampUtc",
                table: "PortfolioSnapshots",
                columns: new[] { "ProductId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioSnapshotItems");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");
        }
    }
}
