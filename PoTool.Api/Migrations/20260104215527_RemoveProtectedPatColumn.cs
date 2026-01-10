using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoTool.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProtectedPatColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove ProtectedPat column from TfsConfigs table if it exists
            // This migration fixes databases that were created from earlier migrations
            // that included ProtectedPat but missed the RemoveProtectedPatFromTfsConfig migration

            // Note: suppressTransaction is required because SQLite doesn't support transactions
            // for DDL operations that involve table recreation (CREATE TABLE, DROP TABLE, ALTER TABLE RENAME)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS TfsConfigs_New (
                    Id INTEGER NOT NULL CONSTRAINT PK_TfsConfigs PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    Project TEXT NOT NULL,
                    ApiVersion TEXT NOT NULL,
                    AuthMode INTEGER NOT NULL,
                    LastValidated TEXT NULL,
                    TimeoutSeconds INTEGER NOT NULL,
                    UseDefaultCredentials INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                
                INSERT INTO TfsConfigs_New (Id, Url, Project, ApiVersion, AuthMode, LastValidated, TimeoutSeconds, UseDefaultCredentials, CreatedAt, UpdatedAt)
                SELECT Id, Url, Project, 
                       COALESCE(ApiVersion, '7.0') as ApiVersion,
                       COALESCE(AuthMode, 0) as AuthMode,
                       LastValidated,
                       COALESCE(TimeoutSeconds, 30) as TimeoutSeconds,
                       COALESCE(UseDefaultCredentials, 0) as UseDefaultCredentials,
                       CreatedAt, UpdatedAt
                FROM TfsConfigs;
                
                DROP TABLE TfsConfigs;
                ALTER TABLE TfsConfigs_New RENAME TO TfsConfigs;
                CREATE INDEX IX_TfsConfigs_Url ON TfsConfigs (Url);
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore ProtectedPat column as the data is lost
            // This is intentional per PAT_STORAGE_BEST_PRACTICES.md
        }
    }
}
