using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoadmapSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadmapSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoadmapSnapshotItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EpicTfsId = table.Column<int>(type: "INTEGER", nullable: false),
                    EpicTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EpicOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadmapSnapshotItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoadmapSnapshotItems_RoadmapSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "RoadmapSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapSnapshotItems_SnapshotId",
                table: "RoadmapSnapshotItems",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadmapSnapshots_CreatedAtUtc",
                table: "RoadmapSnapshots",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoadmapSnapshotItems");

            migrationBuilder.DropTable(
                name: "RoadmapSnapshots");
        }
    }
}
