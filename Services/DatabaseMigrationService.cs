using Microsoft.EntityFrameworkCore;
using SOS.DbData;

namespace SOS.Services
{
    public interface IDatabaseMigrationService
    {
        Task ApplyCustomMigrationsAsync();
    }

    /// <summary>
    /// SOS'a özgü şema migration'larını uygular. EF Migration yerine raw SQL + IF NOT EXISTS pattern.
    /// Yeni tablo / kolon eklemek için bu sınıfa yeni ExecuteSqlAsync çağrısı ekle.
    /// </summary>
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
                _logger.LogInformation("Starting SOS database migrations...");

                // ── TBLSOS_ANA_URUN: 8 ana ürün kategorisi ──
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TBLSOS_ANA_URUN') " +
                    "CREATE TABLE TBLSOS_ANA_URUN (" +
                    "  Id INT NOT NULL PRIMARY KEY, " +
                    "  Kod NVARCHAR(50) NOT NULL, " +
                    "  Ad NVARCHAR(100) NOT NULL, " +
                    "  Sira INT NOT NULL DEFAULT 0, " +
                    "  Aktif BIT NOT NULL DEFAULT 1" +
                    ")");

                // ── TBLSOS_URUN_ESLESTIRME: StockCode → AnaUrun ──
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TBLSOS_URUN_ESLESTIRME') " +
                    "CREATE TABLE TBLSOS_URUN_ESLESTIRME (" +
                    "  Id INT IDENTITY(1,1) PRIMARY KEY, " +
                    "  StokKodu NVARCHAR(128) NOT NULL, " +
                    "  UrunAdi NVARCHAR(512) NULL, " +
                    "  Mask NVARCHAR(20) NULL, " +
                    "  LisansTipi NVARCHAR(50) NULL, " +
                    "  AnaUrunId INT NOT NULL REFERENCES TBLSOS_ANA_URUN(Id)" +
                    ")");

                // ── TBLSOS_HEDEF_AYLIK: Aylık hedefler ──
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TBLSOS_HEDEF_AYLIK') " +
                    "CREATE TABLE TBLSOS_HEDEF_AYLIK (" +
                    "  Id INT IDENTITY(1,1) PRIMARY KEY, " +
                    "  Yil INT NOT NULL, " +
                    "  Ay INT NOT NULL, " +
                    "  Tip NVARCHAR(20) NOT NULL DEFAULT 'GENEL', " +
                    "  AnaUrunId INT NULL REFERENCES TBLSOS_ANA_URUN(Id), " +
                    "  HedefTutar MONEY NOT NULL, " +
                    "  Aktif BIT NOT NULL DEFAULT 1" +
                    ")");

                // ── Seed: 8 ana ürün kategorisi ──
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_ANA_URUN) " +
                    "INSERT INTO TBLSOS_ANA_URUN (Id, Kod, Ad, Sira, Aktif) VALUES " +
                    "(1, 'BFG', N'BFG', 1, 1), (2, 'E_DONUSUM', N'E-Dönüşüm', 2, 1), (3, 'ENROUTE', N'Enroute', 3, 1), (4, 'HOSTING', N'Hosting', 4, 1), (5, 'QUEST', N'Quest', 5, 1), (6, 'SERVICECORE', N'ServiceCore', 6, 1), (7, 'STOKBAR', N'Stokbar', 7, 1), (8, 'VARUNA', N'Varuna', 8, 1)");

                // ── Seed: Ürün eşleştirme — Excel kaynak bazlı ──
                // Sadece boşsa seed et (runtime'da Excel'den eklenen kodlar korunur)
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME) " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'500001210', N'Hakediş Bedeli', N'(özel)', N'', 1), (N'PP.01.002', N'Panorama Platform 8 (P8) Geçiş Lisansı', N'PP', N'Lisans', 3), (N'UniDox', N'UniDox Connector Lisansı', N'(özel)', N'', 2), (N'SM.01.003', N'Logo/Netsis Entegrasyonu', N'SM', N'Yazılım', 7), (N'SMH.02.001', N'StockMate Yazılım Bakımı Hizmeti', N'SMH', N'Hizmet', 7), (N'SMY.01.001', N'StockMate Depo Stok Yönetimi Yazılımı', N'SMY', N'Yazılım', 7), (N'SMY.01.002', N'StockMate Dağıtım Kanalı / Tesis Lisansı', N'SMY', N'Yazılım', 7), (N'SMY.01.003', N'StockMate Ek Kullanıcı Lisansı (+1)', N'SMY', N'Yazılım', 7), (N'SMY.01.004', N'StockMate Ek Kullanıcı Lisansı (+5)', N'SMY', N'Yazılım', 7), (N'SMY.02.001', N'StockMate Mobile Depo Personeli El Terminali Lisansı', N'SMY', N'Yazılım', 7), (N'UH.01.002', N'Unidox Kontör', N'UH', N'Kontör', 2), (N'zzzUH.01.002', N'Unidox Kontör-HATALI KOD', N'zzzUH', N'Kontör', 2), (N'500001765', N'EnRoute Pan.Dağıtım Kan.(Bayi/Şb) Lis PX', N'(özel)', N'', 3), (N'EH.01.001', N'EnRoute Panorama - Proje ve Ürün Yönetimi Danışmanlığı Hizmeti', N'EH', N'Hizmet', 3), (N'EH.01.002', N'EnRoute Panorama - Proje Destek Uzmanı Hizmeti', N'EH', N'Hizmet', 3), (N'EH.01.003', N'EnRoute Panorama - Kurulum ve Eğitim Hizmeti', N'EH', N'Hizmet', 3), (N'EH.01.005', N'EnRoute Panorama - Online Eğitim Hizmeti', N'EH', N'Hizmet', 3), (N'EH.01.006', N'EnRoute Panorama - Süreç Danışmanlığı Hizmeti', N'EH', N'Hizmet', 3), (N'EH.02.001', N'EnRoute Panorama - Yazılım Bakımı, Yaşatma ve Merkezi Destek Hizmeti', N'EH', N'Hizmet', 3), (N'EH.02.004', N'EnRoute Panorama FundManager Module - Fon ve Bütçe Yönetimi Yazılım Bakımı Hizmeti', N'EH', N'Hizmet', 3), (N'EH.02.005', N'ENROUTE PANORAMA CHANNEL BALANCE MODULE - BAYİ STOK DENGELEME YAZILIM BAKIMI HİZMETİ', N'EH', N'Hizmet', 3), (N'EH.02.009', N'ENROUTE P ASSET TRACKER DEMİRBAŞ YAZ. BH', N'EH', N'Hizmet', 3), (N'EH.02.013', N'EnRoute Panorama Business Analytics (QSENSE) Site Lisansı Bakımı', N'EH', N'Hizmet', 3), (N'EH.02.014', N'ENROUTE PANORAMA BUSİNESS ANALYTİCS  (QSENSE) KULLANICI LİSANSI BAKIMI', N'EH', N'Hizmet', 3), (N'EH.02.016', N'EnRoute - E-Defter Saklama Hizmeti', N'EH', N'Hizmet', 2)");

                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME WHERE StokKodu = N'EH.02.018') " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'EH.02.018', N'ENROUTE PANORAMA WEBCONNECTOR BAKIM HİZM', N'EH', N'Hizmet', 3), (N'EH.02.098', N'EnRoute Panorama Veri Çekme Bakım Çağrı Merkezi Destek Hizmeti', N'EH', N'Hizmet', 3), (N'EH.03.001', N'EnRoute Panorama - Çağrı Merkezi Hizmeti (MSD)', N'EH', N'Hizmet', 3), (N'EH.03.003', N'EnRoute Panorama - Uzman Destek Hizmeti', N'EH', N'Hizmet', 3), (N'EH.03.006', N'EnRoute Panorama - Uzaktan Kurulum ve Eğitim Hizmeti', N'EH', N'Hizmet', 3), (N'EH.03.008', N'EnRoute Panorama - Çağrı Merkezi Hizmeti (DDI / OutSource)', N'EH', N'Hizmet', 3), (N'EH.03.011', N'Enroute Panorama - E-Dönüşüm Modülü Kurulum Hizmeti', N'EH', N'Hizmet', 2), (N'EH.05.001', N'EnRoute Panorama - Yazılım Geliştirme Hizmeti', N'EH', N'Hizmet', 3), (N'EH.05.002', N'EnRoute Panorama - Rapor Geliştirme Hizmeti', N'EH', N'Hizmet', 9), (N'EH.06.001', N'EnRoute Panorama - Hosting Hizmeti', N'EH', N'Hizmet', 4), (N'EY.01.002', N'EnRoute Panorama Mobile Sales & Distribution Module (DDI)', N'EY', N'Yazılım', 3), (N'EY.01.011', N'EnRoute Panorama - Dağıtım Kanalı \"Web Connector\"  3rd Party Entegrarasyon Uygulama Lisansı', N'EY', N'Yazılım', 3), (N'EY.01.011 PX', N'EnRoute PX- WebConnector 3rdPrtyEntUygLn', N'EY', N'Yazılım', 3), (N'EY.01.014', N'EnRoute Panorama - Platform Back Office Kullanıcı Lisansı', N'EY', N'Yazılım', 3), (N'EY.01.021', N'EnRoute Panorama - Dağıtım Kanalı (Bayi/Distribütör/Şube) Lisansı', N'EY', N'Yazılım', 3), (N'EY.01.025', N'EnRoute Panorama - Dağıtım Kanalı  \"Web Connector\" Web Service Lisansı', N'EY', N'Yazılım', 3), (N'EY.02.011', N'EnRoute Panorama - Panel - Çoklu Proje Birleştirme Mobil Uygulama Lisansı', N'EY', N'Yazılım', 3), (N'EY.02.011 PX', N'EnRoute PX-Panel Çok PrjBir. MobUy Lsn E', N'EY', N'Yazılım', 3), (N'EY.03.001', N'ENROUTE PANORAMA PAAS LİSANSI (KİRALAMA HİZMETİ)', N'EY', N'Yazılım', 3), (N'EY.04.001', N'EnRoute Panorama - Modül Lisansları Kiralama Hizmeti', N'EY', N'Yazılım', 3), (N'EY.04.002', N'EnRoute Panorama - Fund Manager - Fon ve Bütçe Yönetimi Modül Lisansı', N'EY', N'Yazılım', 3), (N'EY.04.002 PX', N'EnRoute PX-Fon Bütçe Yön. Mod. Lsns Ent', N'EY', N'Yazılım', 3), (N'EY.04.005', N'EnRoute Panorama - Dağıtıcı Lisansları ve Hizmetleri Kiralama Hizmeti', N'EY', N'Yazılım', 3), (N'EY.04.006', N'EnRoute Panorama - Kullanıcı Lisansları Kiralama Hizmeti', N'EY', N'Yazılım', 3), (N'EY.04.006 PX', N'0nRoute PX-Kull Lsns Kira Hiz Ent', N'EY', N'Yazılım', 3)");

                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME WHERE StokKodu = N'EY.04.007') " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'EY.04.007', N'EnRoute Panorama - Asset Tracker - Demirbaş Takip Modül Lisansı', N'EY', N'Yazılım', 3), (N'EY.04.012', N'EnRoute Panorama - E-Dönüşüm Modül Lisansı', N'EY', N'Yazılım', 3), (N'EY.04.012 PX', N'EnRoute PX-E-Dönüşüm Modül Lisansı', N'EY', N'Yazılım', 3), (N'EY.04.014', N'EnRoute - E-Dönüşüm Lisans Komisyonu', N'EY', N'Komisyon', 2), (N'EY.05.009', N'EnRoute Panorama - Business Analytics - Qlik Sense Analyzer User Lisansı', N'EY', N'Yazılım', 3), (N'EY.05.010', N'EnRoute Panorama - Business Analytics - Qlik Sense Professional User Lisansı', N'EY', N'Yazılım', 3), (N'EY.05.010 PX', N'EnRoute Pan. BA Qlik Sense Prof. Lis PX', N'EY', N'Yazılım', 3), (N'EYS.01.001', N'EnRoute Panorama - Mobil Satış & Dağıtım Çözüm Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.01.001 PX', N'EnRoute PX-Mobil Satış Dağıtım Çözüm Lsn', N'EYS', N'Yazılım', 3), (N'EYS.01.002', N'EnRoute Panorama Mobile Sales & Distribution Module (DDI)', N'EYS', N'Yazılım', 3), (N'EYS.01.014', N'EnRoute Panorama - Platform Back Office Kullanıcı Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.01.014 PX', N'EnRoute PX Panorama - Platform Back Office', N'EYS', N'Yazılım', 3), (N'EYS.01.021', N'EnRoute Panorama - Dağıtım Kanalı (Bayi/Distribütör/Şube) Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.01.021 PX', N'EnRoute PX-DağtmKnalı Bayi/Distr./Şub Ls', N'EYS', N'Yazılım', 3), (N'EYS.01.025', N'EnRoute Panorama - Dağıtım Kanalı  \"Web Connector\" Web Service Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.02.011', N'EnRoute Panorama - Panel - Çoklu Proje Birleştirme Mobil Uygulama Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.02.033', N'EnRoute Panorama - Mobil Kullanıcı Lisansı', N'EYS', N'Yazılım', 3), (N'EYS.02.033 PX', N'EnRoute PX Panorama - Mobil Kullanıcı Ls', N'EYS', N'Yazılım', 3), (N'EYS.04.003', N'EnRoute Panorama - Kullanıcı Lisansları Kiralama Hizmeti', N'EYS', N'Yazılım', 3), (N'EYS.04.012', N'EnRoute Panorama - E-Dönüşüm Modül Lisansı', N'EYS', N'Yazılım', 2), (N'OH.01.002', N'Outsource Proje Yönetim Hizmeti', N'OH', N'Hizmet', 3), (N'OH.02.001', N'Outsource Yazılım Bakımı Hizmeti', N'OH', N'Hizmet', 3), (N'OH.02.002', N'Outsource Donanım Bakımı Hizmeti', N'OH', N'Hizmet', 4), (N'WPH.02.001', N'WebPlus Yazılım Bakımı Hizmeti', N'WPH', N'Hizmet', 3), (N'WPH.03.001', N'WEBPLUS HOSTİNG HİZMETİ', N'WPH', N'Hizmet', 4)");

                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME WHERE StokKodu = N'WPY.01.001') " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'WPY.01.001', N'WebPlus Distribution Satış-Dağıtım Yazılımı (Enterprise)', N'WPY', N'Yazılım', 3), (N'WPY.01.002', N'WebPlus Dağıtım Kanalı Lisansı', N'WPY', N'Yazılım', 3), (N'WPY.01.003', N'Web Plus Ek Kullanıcı Lisansı (1 Kullanıcı)', N'WPY', N'Yazılım', 3), (N'WPY.01.003 PX', N'Web Pls PX Ek Kull Lsnsı(1 Kull)-Ent.', N'WPY', N'Yazılım', 3), (N'WPY.01.004', N'Web Plus Ek Kullanıcı Lisansı (5 Kullanıcı)', N'WPY', N'Yazılım', 3), (N'WPY.01.004 PX', N'Web Pls PX Ek Kull Lsnsı(5 Kull)', N'WPY', N'Yazılım', 3), (N'WPY.01.005', N'WebPlus Distribution Satış-Dağıtım Yazılımı (Standart)', N'WPY', N'Yazılım', 3), (N'WPY.01.006', N'WebPlus Distribution Satış-Dağıtım Yazılımı (Light)', N'WPY', N'Yazılım', 3), (N'WPY.01.007', N'WebPlus E-Dönüşüm Lisansı', N'WPY', N'Yazılım', 3), (N'WPY.01.008', N'WebPlus Distribution Satış-Dağıtım Yazılımı (Enterprise) Kiralama Hizmeti', N'WPY', N'Yazılım', 3), (N'WPY.02.001', N'WebPlus (Android) Satış Temsilcisi Lisansı', N'WPY', N'Yazılım', 3), (N'WPY.02.001 PX', N'WebPlus  PX (Andrd) Satş Tem. Lsnsı-Ent.', N'WPY', N'Yazılım', 3), (N'WPY.02.002', N'WebPlus DeliveryMan (Dağıtıcı) El Terminali Lisansı - Android', N'WPY', N'Yazılım', 3), (N'WPY.02.003', N'WarehouseMan (Depo Personeli) El Terminali Lisansı - Android', N'WPY', N'Yazılım', 3), (N'WPY.02.008', N'WebPlus (IOS) Satış Temsilcisi Lisansı', N'WPY', N'Yazılım', 3), (N'WPY.04.012', N'WebPlus Enterprise \"Web Connector\" Web Service Lisansı', N'WPY', N'Yazılım', 3), (N'QMY.01.001 PX', N'QuestMt PX- Mobil İş Çözümü', N'(özel)', N'', 5), (N'QH.01.001', N'Quest Panorama - Proje ve Ürün Yönetimi Danışmanlığı Hizmeti', N'QH', N'Hizmet', 5), (N'QH.01.008', N'Quest Panorama - Q-Capture - Nöral Ağ Yeni Kategori Tanımlama (200 SKU)', N'QH', N'Hizmet', 5), (N'QH.01.013', N'Quest Panorama - Q-Capture - Görsel Tanımlama Sunucu Aylık Bakım Hizmeti', N'QH', N'Hizmet', 5), (N'QH.02.001', N'Quest Panorama - Yazılım Bakımı, Yaşatma ve Merkezi Destek Hizmeti', N'QH', N'Hizmet', 5), (N'QH.03.001', N'Quest Panorama  - Çağrı Merkezi Hizmeti', N'QH', N'Hizmet', 5), (N'QH.03.005', N'Quest Panorama - Kullanıcı Lisansları Kiralama Hizmeti (Enterprise)', N'QH', N'Hizmet', 5), (N'QH.06.001', N'Quest Panorama - Yazılım Geliştirme Hizmeti', N'QH', N'Hizmet', 5), (N'QH.07.001', N'Quest Panorama - Rapor Geliştirme Hizmeti', N'QH', N'Hizmet', 9)");

                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME WHERE StokKodu = N'QH.07.002') " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'QH.07.002', N'Quest Panorama - PY Rapor Geliştirme Hizmeti', N'QH', N'Hizmet', 9), (N'QH.08.001', N'Quest Panorama - Hosting Hizmeti', N'QH', N'Hizmet', 4), (N'QMH.02.001', N'QuestMate Yazılım Bakımı Hizmeti', N'QMH', N'Hizmet', 5), (N'QY.02.007', N'Quest Panorama - Mobil Kullanıcısı Lisansı', N'QY', N'Yazılım', 5), (N'QY.04.004', N'Quest Panorama - Q-Auditor Mobil Kullanıcı Lisansı', N'QY', N'Yazılım', 5), (N'QY.05.003', N'Quest Panorama - Business Analytics - İş Zekası Raporlama (MS Power BI) Site Lisansı', N'QY', N'Yazılım', 5), (N'QY.06.001', N'Quest Panorama - Business Analytics  - Qlik Sense Analyzer User Lisansı', N'QY', N'Yazılım', 5), (N'QY.06.002', N'Quest Panorama - Business Analytics  - Qlik Sense Professional User Lisansı', N'QY', N'Yazılım', 5), (N'QYS.01.001', N'Quest Panorama - Veri Toplama & Mobil Ekip Yönetimi Çözüm Lisansı', N'QYS', N'Yazılım', 5), (N'QYS.01.006', N'Quest Panorama - Platform Back Office Kullanıcı Lisansı', N'QYS', N'Yazılım', 5), (N'QYS.02.007', N'Quest Panorama - Mobil Kullanıcısı Lisansı', N'QYS', N'Yazılım', 5), (N'FURKAN-0101', N'CallDesk PX-Dağıtıcı Lisans ve Hzm. Kira.', N'(özel)', N'', 6), (N'CDH.01.001', N'Calldesk Panorama - Proje ve Ürün Yönetimi Danışmanlığı Hizmeti', N'CDH', N'Hizmet', 6), (N'CDH.01.003', N'Calldesk Panorama - Kurulum ve Eğitim Hizmeti', N'CDH', N'Hizmet', 6), (N'CDH.02.001', N'Calldesk Panorama - Yazılım Bakımı Hizmeti', N'CDH', N'Hizmet', 6), (N'CDH.03.001', N'Calldesk Panorama Destek Hizmeti (Çağrı Merkezi)', N'CDH', N'Hizmet', 6), (N'CDH.03.002', N'Calldesk Panorama - Uzman Destek Hizmeti', N'CDH', N'Hizmet', 6), (N'CDH.03.003', N'Calldesk Panorama - Admin Operatör Hizmeti', N'CDH', N'Hizmet', 6), (N'CDY.01.001', N'CallDesk Panorama- Servis Otomasyonu Çözüm Lisansı', N'CDY', N'Yazılım', 6), (N'CDY.01.008', N'CallDesk Panorama - Merkez ERP Entegrasyonu Web Service Lisansı', N'CDY', N'Yazılım', 6), (N'CDY.01.011', N'CallDesk Panorama - Platform Back Office Kullanıcı Lisansı', N'CDY', N'Yazılım', 6), (N'CDY.01.016', N'CallDesk Panorama - Servis Noktası Lisansı', N'CDY', N'Yazılım', 6), (N'SH.02.001', N'StokBar Panorama - Yazılım Bakımı Hizmeti', N'SH', N'Hizmet', 7), (N'SH.03.001', N'StokBar Panorama - Çağrı Merkezi Hizmeti (Tesis)', N'SH', N'Hizmet', 7)");

                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_URUN_ESLESTIRME WHERE StokKodu = N'SH.03.002') " +
                    "INSERT INTO TBLSOS_URUN_ESLESTIRME (StokKodu, UrunAdi, Mask, LisansTipi, AnaUrunId) VALUES " +
                    "(N'SH.03.002', N'StokBar Panorama - Uzman Destek Hizmeti', N'SH', N'Hizmet', 7), (N'SH.03.004', N'StokBar Panorama - Çağrı Merkezi Hizmeti (Depo/Lokasyon)', N'SH', N'Hizmet', 7), (N'SH.03.006', N'StokBar Panorama - Çağrı Başı Destek Hizmeti', N'SH', N'Hizmet', 7), (N'SH.05.001', N'StokBar Panorama - Yazılım Geliştirme Hizmeti', N'SH', N'Hizmet', 7), (N'SH.06.001', N'StokBar Panorama - Hosting Hizmeti', N'SH', N'Hizmet', 4), (N'SH.01.001', N'StokBar Panorama - Proje ve Ürün Yönetimi Danışmanlığı Hizmeti', N'SH.01', N'', 7), (N'SH.01.003', N'StokBar Panorama - Kurulum ve Eğitim Hizmeti', N'SH.01', N'', 7), (N'SY.01.004', N'StokBar Panorama - Depo & Sevkiyat Yönetimi \"Standart\" Çözüm Lisansı', N'SY', N'Yazılım', 7), (N'SY.01.005', N'StokBar Panorama - Depo & Üretim Yönetimi Çözüm Lisansı (Business)', N'SY', N'Yazılım', 7), (N'SY.01.007', N'StokBar Panorama - Tesis Lisansı', N'SY', N'Yazılım', 7), (N'SY.01.008', N'StokBar Panorama - Depo / Lokasyon Lisansı', N'SY', N'Yazılım', 7), (N'SY.01.009', N'Stokbar Panorama - Platform Back Office Kullanıcı Lisansı', N'SY', N'Yazılım', 7), (N'SY.02.002', N'StokBar Panorama - Mobil (Android) Kullanıcı Lisansı', N'SY', N'Yazılım', 7), (N'SY.03.016', N'StokBar Panorama - İş Atama Modülü Lisansı', N'SY', N'Yazılım', 7), (N'VH.01.001', N'Varuna SSH Proje Yönetim Hizmeti', N'VH', N'Hizmet', 8), (N'VH.01.002', N'Varuna SSH Yazılım Geliştirme Hizmeti', N'VH', N'Hizmet', 8), (N'VH.01.004', N'Varuna SSH Entegrasyon Hizmeti', N'VH', N'Hizmet', 8), (N'VY.01.005', N'Varuna SSH (Starter) - Teknisyen Aylık Kiralama', N'VY', N'Yazılım', 8), (N'VY.04.006', N'Varuna SSH (Enterprise) - Teknisyen Yıllık  Kiralama', N'VY', N'Yazılım', 8), (N'VY.05.007', N'Varuna CRM (Enterprise) Aylık Kiralama', N'VY', N'Yazılım', 8)");

                // ── Seed: 2026 genel hedefler (Excel onaylı, toplam ₺600M) ──
                await ExecuteSqlAsync(
                    "IF NOT EXISTS (SELECT 1 FROM TBLSOS_HEDEF_AYLIK WHERE Yil=2026 AND Tip='GENEL') " +
                    "INSERT INTO TBLSOS_HEDEF_AYLIK (Yil, Ay, Tip, AnaUrunId, HedefTutar, Aktif) VALUES " +
                    "(2026, 1, 'GENEL', NULL, 42000000, 1), (2026, 2, 'GENEL', NULL, 42000000, 1), (2026, 3, 'GENEL', NULL, 48000000, 1), (2026, 4, 'GENEL', NULL, 45000000, 1), (2026, 5, 'GENEL', NULL, 50000000, 1), (2026, 6, 'GENEL', NULL, 55000000, 1), (2026, 7, 'GENEL', NULL, 50000000, 1), (2026, 8, 'GENEL', NULL, 55000000, 1), (2026, 9, 'GENEL', NULL, 50000000, 1), (2026, 10, 'GENEL', NULL, 55000000, 1), (2026, 11, 'GENEL', NULL, 53000000, 1), (2026, 12, 'GENEL', NULL, 55000000, 1)");

                _logger.LogInformation("SOS database migrations completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SOS database migrations failed - this may be due to permissions or migrations already applied");
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
