using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBacklogRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductBacklogRoots",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkItemTfsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBacklogRoots", x => new { x.ProductId, x.WorkItemTfsId });
                    table.ForeignKey(
                        name: "FK_ProductBacklogRoots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Migrate existing BacklogRootWorkItemId values to the new join table
            migrationBuilder.Sql(
                @"INSERT INTO ProductBacklogRoots (ProductId, WorkItemTfsId)
                  SELECT Id, BacklogRootWorkItemId
                  FROM Products
                  WHERE BacklogRootWorkItemId IS NOT NULL AND BacklogRootWorkItemId > 0");

            migrationBuilder.DropColumn(
                name: "BacklogRootWorkItemId",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductBacklogRoots");

            migrationBuilder.AddColumn<int>(
                name: "BacklogRootWorkItemId",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
