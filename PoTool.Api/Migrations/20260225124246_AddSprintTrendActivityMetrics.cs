using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintTrendActivityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BugsClosedCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BugsCreatedCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletedPbiCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletedPbiEffort",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproximate",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MissingEffortCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ProgressionDelta",
                table: "SprintMetricsProjections",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BugsClosedCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "BugsCreatedCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "CompletedPbiCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "CompletedPbiEffort",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "IsApproximate",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "MissingEffortCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "ProgressionDelta",
                table: "SprintMetricsProjections");
        }
    }
}
