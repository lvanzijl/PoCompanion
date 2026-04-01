using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsWithAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "ProjectId",
                table: "Products",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            if (migrationBuilder.ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    WITH normalized AS (
                        SELECT
                            ProductId,
                            ProductName,
                            CASE
                                WHEN BaseAlias = '' THEN 'project'
                                ELSE BaseAlias
                            END AS EffectiveBaseAlias,
                            ROW_NUMBER() OVER (
                                PARTITION BY CASE
                                    WHEN BaseAlias = '' THEN 'project'
                                    ELSE BaseAlias
                                END
                                ORDER BY ProductId
                            ) AS AliasSequence
                        FROM (
                            SELECT
                                Id AS ProductId,
                                Name AS ProductName,
                                TRIM(
                                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                            LOWER(Name),
                                            ' ', '-'),
                                            '/', '-'),
                                            '\', '-'),
                                            '_', '-'),
                                            '.', '-'),
                                            ',', '-'),
                                            '&', '-'),
                                            '+', '-'),
                                            ':', '-'),
                                            ';', '-'),
                                            '(', '-'),
                                            ')', '-'),
                                            '[', '-'),
                                            ']', '-'),
                                            '{', '-'),
                                            '}', '-'),
                                            '--', '-'),
                                            '--', '-')
                                , '-') AS BaseAlias
                            FROM Products
                        )
                    )
                    INSERT INTO Projects (Id, Alias, Name)
                    SELECT
                        'project-' || ProductId,
                        CASE
                            WHEN AliasSequence = 1 THEN EffectiveBaseAlias
                            ELSE EffectiveBaseAlias || '-' || AliasSequence
                        END,
                        ProductName
                    FROM normalized;

                    UPDATE Products
                    SET ProjectId = 'project-' || Id
                    WHERE ProjectId = '';
                    """);
            }
            else
            {
                migrationBuilder.Sql("""
                    WITH normalized AS (
                        SELECT
                            ProductId,
                            ProductName,
                            CASE
                                WHEN BaseAlias = '' THEN 'project'
                                ELSE BaseAlias
                            END AS EffectiveBaseAlias,
                            ROW_NUMBER() OVER (
                                PARTITION BY CASE
                                    WHEN BaseAlias = '' THEN 'project'
                                    ELSE BaseAlias
                                END
                                ORDER BY ProductId
                            ) AS AliasSequence
                        FROM (
                            SELECT
                                Id AS ProductId,
                                Name AS ProductName,
                                TRIM('-' FROM REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                                        LOWER(Name),
                                        ' ', '-'),
                                        '/', '-'),
                                        '\', '-'),
                                        '_', '-'),
                                        '.', '-'),
                                        ',', '-'),
                                        '&', '-'),
                                        '+', '-'),
                                        ':', '-'),
                                        ';', '-'),
                                        '(', '-'),
                                        ')', '-'),
                                        '[', '-'),
                                        ']', '-'),
                                        '{', '-'),
                                        '}', '-'),
                                        '--', '-'),
                                        '--', '-')) AS BaseAlias
                            FROM Products
                        ) source
                    )
                    INSERT INTO Projects (Id, Alias, Name)
                    SELECT
                        CONCAT('project-', ProductId),
                        CASE
                            WHEN AliasSequence = 1 THEN EffectiveBaseAlias
                            ELSE CONCAT(EffectiveBaseAlias, '-', AliasSequence)
                        END,
                        ProductName
                    FROM normalized;

                    UPDATE Products
                    SET ProjectId = CONCAT('project-', Id)
                    WHERE ProjectId = '';
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProjectId",
                table: "Products",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Alias",
                table: "Projects",
                column: "Alias",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Projects_ProjectId",
                table: "Products",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Projects_ProjectId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProjectId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Products");
        }
    }
}
