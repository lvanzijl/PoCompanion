using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotAndProjectionTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RelationshipsSnapshotAsOfUtc",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RelationshipsSnapshotWorkItemWatermark",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolutionAsOfUtc",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SprintTrendProjectionAsOfUtc",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemRelationshipEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductOwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceWorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetWorkItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    RelationType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SnapshotAsOfUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemRelationshipEdges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemRelationshipEdges_ProductOwnerId_SourceWorkItemId",
                table: "WorkItemRelationshipEdges",
                columns: new[] { "ProductOwnerId", "SourceWorkItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemRelationshipEdges_ProductOwnerId_TargetWorkItemId",
                table: "WorkItemRelationshipEdges",
                columns: new[] { "ProductOwnerId", "TargetWorkItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemRelationshipEdges");

            migrationBuilder.DropColumn(
                name: "RelationshipsSnapshotAsOfUtc",
                table: "ProductOwnerCacheStates");

            migrationBuilder.DropColumn(
                name: "RelationshipsSnapshotWorkItemWatermark",
                table: "ProductOwnerCacheStates");

            migrationBuilder.DropColumn(
                name: "ResolutionAsOfUtc",
                table: "ProductOwnerCacheStates");

            migrationBuilder.DropColumn(
                name: "SprintTrendProjectionAsOfUtc",
                table: "ProductOwnerCacheStates");
        }
    }
}
