using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRunCreatedDateUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateUtc",
                table: "CachedPipelineRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE CachedPipelineRuns
                SET CreatedDateUtc = CASE
                    WHEN CreatedDate IS NULL THEN NULL
                    ELSE datetime(CreatedDate)
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CachedPipelineRuns_CreatedDateUtc",
                table: "CachedPipelineRuns",
                column: "CreatedDateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CachedPipelineRuns_CreatedDateUtc",
                table: "CachedPipelineRuns");

            migrationBuilder.DropColumn(
                name: "CreatedDateUtc",
                table: "CachedPipelineRuns");
        }
    }
}
