using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemCreatedDateUtcForSqliteBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDateUtc",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("""
                    UPDATE WorkItems
                    SET CreatedDateUtc = CASE
                        WHEN CreatedDate IS NULL THEN NULL
                        ELSE datetime(CreatedDate)
                    END;
                    """);
            }
            else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    UPDATE WorkItems
                    SET CreatedDateUtc = CASE
                        WHEN CreatedDate IS NULL THEN NULL
                        ELSE CONVERT(datetime2, SWITCHOFFSET(CreatedDate, '+00:00'))
                    END;
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_CreatedDateUtc",
                table: "WorkItems",
                column: "CreatedDateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_CreatedDateUtc",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CreatedDateUtc",
                table: "WorkItems");
        }
    }
}
