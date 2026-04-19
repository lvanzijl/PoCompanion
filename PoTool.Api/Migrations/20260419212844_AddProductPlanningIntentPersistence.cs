using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPlanningIntentPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartDate",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateUtc",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TargetDate",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetDateUtc",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductPlanningIntents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    EpicId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartSprintStartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationInSprints = table.Column<int>(type: "INTEGER", nullable: false),
                    RecoveryStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPlanningIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPlanningIntents_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPlanningIntents_ProductId_EpicId",
                table: "ProductPlanningIntents",
                columns: new[] { "ProductId", "EpicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPlanningIntents_ProductId_StartSprintStartDateUtc",
                table: "ProductPlanningIntents",
                columns: new[] { "ProductId", "StartSprintStartDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPlanningIntents");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StartDateUtc",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TargetDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TargetDateUtc",
                table: "WorkItems");
        }
    }
}
