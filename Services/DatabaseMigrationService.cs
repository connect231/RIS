using Microsoft.EntityFrameworkCore;
using UniCP.DbData;

namespace UniCP.Services
{
    public interface IDatabaseMigrationService
    {
        Task ApplyCustomMigrationsAsync();
    }

    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly MskDbContext _context;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(MskDbContext context, ILogger<DatabaseMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ApplyCustomMigrationsAsync()
        {
            try
            {
                _logger.LogInformation("Starting custom database migrations...");

                // Migration 1: Add TXT_PO column
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_TALEP' AND COLUMN_NAME = 'TXT_PO') " +
                    "ALTER TABLE TBL_TALEP ADD TXT_PO VARCHAR(50)");

                // Migration 2: Add TRHKAYIT column
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_TALEP' AND COLUMN_NAME = 'TRHKAYIT') " +
                    "ALTER TABLE TBL_TALEP ADD TRHKAYIT DATETIME");

                // Migration 3: Add INT_ANKET_PUAN column
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_TALEP' AND COLUMN_NAME = 'INT_ANKET_PUAN') " +
                    "ALTER TABLE TBL_TALEP ADD INT_ANKET_PUAN INT");

                // Migration 4: Add TXT_ANKET_NOT column
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_TALEP' AND COLUMN_NAME = 'TXT_ANKET_NOT') " +
                    "ALTER TABLE TBL_TALEP ADD TXT_ANKET_NOT VARCHAR(500)");

                // Migration 5: Change TXT_ACIKLAMA to NVARCHAR
                await ExecuteSqlAsync(
                    "IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_TALEP' AND COLUMN_NAME = 'TXT_ACIKLAMA' AND DATA_TYPE = 'varchar') " +
                    "ALTER TABLE TBL_TALEP ALTER COLUMN TXT_ACIKLAMA NVARCHAR(MAX)");

                _logger.LogInformation("Custom database migrations completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Custom database migrations failed - this may be due to permissions or migrations already applied");
                // Don't throw - allow application to start even if migrations fail
            }
        }

        private async Task ExecuteSqlAsync(string sql)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogDebug("Executed migration: {Sql}", sql.Substring(0, Math.Min(50, sql.Length)) + "...");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Migration SQL failed: {Sql}", sql.Substring(0, Math.Min(50, sql.Length)));
                // Continue with other migrations even if one fails
            }
        }
    }
}
