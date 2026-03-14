using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintProjectionSpilloverMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpilloverCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpilloverEffort",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpilloverCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "SpilloverEffort",
                table: "SprintMetricsProjections");
        }
    }
}
