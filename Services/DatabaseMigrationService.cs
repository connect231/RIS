using Microsoft.EntityFrameworkCore;
using SOS.DbData;

namespace SOS.Services
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

                // Migration 6: Add LNGORTAKFIRMAKOD to TBL_VARUNA_SOZLESME
                // Bu kolon, sözleşmeleri portal firma numarasıyla doğrudan ilişkilendirir.
                // String tabanlı AccountTitle eşleştirmesinin yerini alır.
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_VARUNA_SOZLESME' AND COLUMN_NAME = 'LNGORTAKFIRMAKOD') " +
                    "ALTER TABLE TBL_VARUNA_SOZLESME ADD LNGORTAKFIRMAKOD INT NULL");

                // Migration 7: LNGORTAKFIRMAKOD kolonunu tek eşleşen kayıtlar için otomatik doldur.
                // COUNT=1 koşulu: birden fazla firma adıyla eşleşen sözleşmeleri atlar (yanlış atama riski).
                // Güvenli: NULL olmayan kayıtlara dokunmaz, tekrarlı çalışmada idempotent'tir.
                await ExecuteSqlAsync(
                    "UPDATE s SET s.LNGORTAKFIRMAKOD = p.LNGKOD " +
                    "FROM TBL_VARUNA_SOZLESME s " +
                    "CROSS JOIN VIEW_ORTAK_PROJE_ISIMLERI p " +
                    "WHERE s.LNGORTAKFIRMAKOD IS NULL " +
                    "  AND s.AccountTitle IS NOT NULL " +
                    "  AND p.TXTORTAKPROJEADI IS NOT NULL " +
                    "  AND s.AccountTitle LIKE '%' + p.TXTORTAKPROJEADI + '%' " +
                    "  AND (SELECT COUNT(*) FROM VIEW_ORTAK_PROJE_ISIMLERI p2 " +
                    "       WHERE p2.TXTORTAKPROJEADI IS NOT NULL " +
                    "         AND s.AccountTitle LIKE '%' + p2.TXTORTAKPROJEADI + '%') = 1");

                // Migration 8: Add LNGORTAKFIRMAKOD to TBL_SISTEM_LOGs
                // Hangi kullanıcının hangi tenant bağlamında işlem yaptığını kaydeder.
                // Veri sızıntısı soruşturmalarında forensic analiz için kritik.
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TBL_SISTEM_LOGs' AND COLUMN_NAME = 'LNGORTAKFIRMAKOD') " +
                    "ALTER TABLE TBL_SISTEM_LOGs ADD LNGORTAKFIRMAKOD INT NULL");

                // Migration 9: TBL_DUYURU tablosu — PDF base64 olarak saklanır
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TBL_DUYURU') " +
                    "CREATE TABLE TBL_DUYURU (" +
                    "  Id INT IDENTITY(1,1) PRIMARY KEY, " +
                    "  PdfDosyaAdi NVARCHAR(255) NOT NULL, " +
                    "  PdfIcerik NVARCHAR(MAX) NOT NULL, " +
                    "  BaslangicTarihi DATETIME NOT NULL, " +
                    "  BitisTarihi DATETIME NOT NULL, " +
                    "  EklenmeTarihi DATETIME NOT NULL DEFAULT GETDATE(), " +
                    "  EkleyenKullaniciId INT NULL" +
                    ")");

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

