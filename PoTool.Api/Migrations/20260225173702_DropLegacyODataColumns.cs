using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyODataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalyticsODataBaseUrl",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "AnalyticsODataEntitySetPath",
                table: "TfsConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalyticsODataBaseUrl",
                table: "TfsConfigs",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AnalyticsODataEntitySetPath",
                table: "TfsConfigs",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }
    }
}
