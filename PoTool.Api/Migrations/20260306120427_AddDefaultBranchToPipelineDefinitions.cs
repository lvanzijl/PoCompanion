using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultBranchToPipelineDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultBranch",
                table: "PipelineDefinitions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultBranch",
                table: "PipelineDefinitions");
        }
    }
}
