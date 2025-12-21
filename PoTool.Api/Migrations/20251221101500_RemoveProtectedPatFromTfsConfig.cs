using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Removes ProtectedPat column from TfsConfigs table.
    /// PAT is now stored client-side using MAUI SecureStorage for improved security.
    /// See docs/PAT_STORAGE_BEST_PRACTICES.md for details.
    /// </summary>
    public partial class RemoveProtectedPatFromTfsConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedPat",
                table: "TfsConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedPat",
                table: "TfsConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}
