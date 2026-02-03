using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameFirstObservedCriticalityToSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FirstObservedCriticality",
                table: "BugTriageStates",
                newName: "FirstObservedSeverity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FirstObservedSeverity",
                table: "BugTriageStates",
                newName: "FirstObservedCriticality");
        }
    }
}
