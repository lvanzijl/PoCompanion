using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStartupReadinessAndProfilePictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasTestedConnectionSuccessfully",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasVerifiedTfsApiSuccessfully",
                table: "TfsConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CustomPicturePath",
                table: "Profiles",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPictureId",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PictureType",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasTestedConnectionSuccessfully",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "HasVerifiedTfsApiSuccessfully",
                table: "TfsConfigs");

            migrationBuilder.DropColumn(
                name: "CustomPicturePath",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "DefaultPictureId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "PictureType",
                table: "Profiles");
        }
    }
}
