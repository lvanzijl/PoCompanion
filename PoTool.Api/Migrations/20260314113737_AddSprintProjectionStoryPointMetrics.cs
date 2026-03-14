using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintProjectionStoryPointMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompletedPbiStoryPoints",
                table: "SprintMetricsProjections",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DerivedStoryPointCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DerivedStoryPoints",
                table: "SprintMetricsProjections",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "MissingStoryPointCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "PlannedStoryPoints",
                table: "SprintMetricsProjections",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpilloverStoryPoints",
                table: "SprintMetricsProjections",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "UnestimatedDeliveryCount",
                table: "SprintMetricsProjections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedPbiStoryPoints",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "DerivedStoryPointCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "DerivedStoryPoints",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "MissingStoryPointCount",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "PlannedStoryPoints",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "SpilloverStoryPoints",
                table: "SprintMetricsProjections");

            migrationBuilder.DropColumn(
                name: "UnestimatedDeliveryCount",
                table: "SprintMetricsProjections");
        }
    }
}
