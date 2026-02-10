using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionWatermarkFallbackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FallbackResumeIndex",
                table: "RevisionIngestionWatermarks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackUsedLastRun",
                table: "RevisionIngestionWatermarks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastStableChangedDateUtc",
                table: "RevisionIngestionWatermarks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastStableContinuationTokenHash",
                table: "RevisionIngestionWatermarks",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackResumeIndex",
                table: "RevisionIngestionWatermarks");

            migrationBuilder.DropColumn(
                name: "FallbackUsedLastRun",
                table: "RevisionIngestionWatermarks");

            migrationBuilder.DropColumn(
                name: "LastStableChangedDateUtc",
                table: "RevisionIngestionWatermarks");

            migrationBuilder.DropColumn(
                name: "LastStableContinuationTokenHash",
                table: "RevisionIngestionWatermarks");
        }
    }
}
