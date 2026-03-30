using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineFinishWatermark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PipelineFinishWatermark",
                table: "ProductOwnerCacheStates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PipelineFinishWatermark",
                table: "ProductOwnerCacheStates");
        }
    }
}
