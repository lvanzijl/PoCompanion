using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TfsId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AreaPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IterationPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JsonPayload = table.Column<string>(type: "TEXT", nullable: false),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AreaPath",
                table: "WorkItems",
                column: "AreaPath");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_RetrievedAt",
                table: "WorkItems",
                column: "RetrievedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_TfsId",
                table: "WorkItems",
                column: "TfsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Type",
                table: "WorkItems",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItems");
        }
    }
}
