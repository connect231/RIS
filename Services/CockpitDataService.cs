using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using SOS.DbData;

namespace SOS.Services;

#region Models

public class FaturaRow
{
    public string FaturaNo { get; set; } = "";
    public DateTime EfektifTarih { get; set; }
    public decimal NetTutar { get; set; }
    public string? Firma { get; set; }
    public int VarunaEslesti { get; set; }
    public int TahakkukVar { get; set; }
    public int IsSentetik { get; set; }
}

public class FaturaOzet
{
    public decimal Toplam { get; set; }
    public int Adet { get; set; }
    public decimal SentetikToplam { get; set; }
    public int SentetikAdet { get; set; }
}

public class TahsilatOzet
{
    public decimal TahsilEdilen { get; set; }
    public int TahsilAdet { get; set; }
    public decimal BekleyenBakiyeToplam { get; set; }
    public decimal VadesiGelenToplam { get; set; }
    public int VadesiGelenAdet { get; set; }
}

public class SozlesmeRow
{
    public Guid? Id { get; set; }
    public string? ContractNo { get; set; }
    public string? ContractName { get; set; }
    public string? ContractStatus { get; set; }
    public string? Firma { get; set; }
    public decimal? EskiTutar { get; set; }
    public decimal? EskiTutarLocal { get; set; }
    public DateTime? EskiBitis { get; set; }
    public DateTime? Yenilemetarihi { get; set; }
    public int Yenilendi { get; set; }
    public string? YeniContractNo { get; set; }
    public string? YeniStatus { get; set; }
    public decimal? YeniTutar { get; set; }
    public decimal? YeniTutarLocal { get; set; }
    public DateTime? YeniBaslangic { get; set; }
    public DateTime? YeniBitis { get; set; }
    public string? YeniInvoiceStatusId { get; set; }
    public string? FaturaStatu { get; set; }
}

public class SozlesmeOzet
{
    public int Toplam { get; set; }
    public int YenilenenAdet { get; set; }
    public int BekleyenAdet { get; set; }
    public decimal EskiTutar { get; set; }
    /// <summary>Hedef: InvoiceStatusId=Tamamlandı olan yeni sözleşmelerin tutarı</summary>
    public decimal YeniTutar { get; set; }
    public decimal BekleyenTutar { get; set; }
    /// <summary>Gerçekleşen: sadece Archived yeni sözleşmelerin tutarı</summary>
    public decimal ArchivedTutar { get; set; }
    public int ArchivedAdet { get; set; }
    /// <summary>Faturalandı (InvoiceStatusId=Tamamlandı) tutar + adet</summary>
    public decimal FaturalandiTutar { get; set; }
    public int FaturalandiAdet { get; set; }
    /// <summary>Kısmi Faturalandı (InvoiceStatusId=Kısmi)</summary>
    public decimal KismiFaturalandiTutar { get; set; }
    public int KismiFaturalandiAdet { get; set; }
    /// <summary>Fesih/İptal (ContractStatus=TerminationCancellation) yeni sözleşme</summary>
    public decimal FesihTutar { get; set; }
    public int FesihAdet { get; set; }
    public List<string> FesihFirmalar { get; set; } = new();
}

public class PipelineResult
{
    public int TumFirsatAdet { get; set; }
    public decimal TumFirsatTutar { get; set; }
    public int FirsatAdet { get; set; }
    public decimal FirsatTutar { get; set; }
    public int TeklifAdet { get; set; }
    public decimal TeklifTutar { get; set; }
    public int AcikSiparisAdet { get; set; }
    public decimal AcikSiparisTutar { get; set; }
    public int KapaliSiparisAdet { get; set; }
    public decimal KapaliSiparisTutar { get; set; }
    public int DonemFirsatAdet { get; set; }
}

#endregion

public interface ICockpitDataService
{
    Task<List<FaturaRow>> GetFaturalarAsync(DateTime start, DateTime end, string? owner = null);
    Task<FaturaOzet> GetFaturaOzetAsync(DateTime start, DateTime end, string? owner = null);
    Task<TahsilatOzet> GetTahsilatOzetAsync(DateTime start, DateTime end);
    Task<List<SozlesmeRow>> GetSozlesmelerAsync(DateTime start, DateTime end);
    Task<SozlesmeOzet> GetSozlesmeOzetAsync(DateTime start, DateTime end);
    Task<PipelineResult> GetPipelineAsync(DateTime start, DateTime end, string? owner = null);
    void InvalidateAll();
}

public class CockpitDataService : ICockpitDataService
{
    private readonly IDbContextFactory<MskDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private static readonly TimeSpan TTL = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> _keys = new();

    public CockpitDataService(IDbContextFactory<MskDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    // ── FATURA ──

    public async Task<List<FaturaRow>> GetFaturalarAsync(DateTime start, DateTime end, string? owner = null)
    {
        var ownerKey = owner ?? "all";
        var key = $"sp_fat_{start:yyyyMMdd}_{end:yyyyMMdd}_{ownerKey}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            db.Database.SetCommandTimeout(60);
            return await db.Database.SqlQueryRaw<FaturaRow>(
                "EXEC SP_COCKPIT_FATURA @p0, @p1, @p2", start, end, owner as object ?? DBNull.Value).ToListAsync();
        });
    }

    public async Task<FaturaOzet> GetFaturaOzetAsync(DateTime start, DateTime end, string? owner = null)
    {
        var rows = await GetFaturalarAsync(start, end, owner);
        return new FaturaOzet
        {
            Toplam = rows.Sum(r => r.NetTutar),
            Adet = rows.Count,
            SentetikToplam = rows.Where(r => r.IsSentetik == 1).Sum(r => r.NetTutar),
            SentetikAdet = rows.Count(r => r.IsSentetik == 1)
        };
    }

    // ── TAHSİLAT ──

    public async Task<TahsilatOzet> GetTahsilatOzetAsync(DateTime start, DateTime end)
    {
        // Tarih normalizasyonu: SP DATE parametreleri — saat kısmı atılır
        var startDate = start.Date;
        var endDate = end.Date;
        var key = $"sp_tah_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            db.Database.SetCommandTimeout(60);
            // SP_COCKPIT_TAHSILAT: deduplicate + Bekleyen_Bakiye fallback + Hukuki/İade filtresi
            var rows = await db.Database.SqlQueryRaw<TahsilatOzet>(
                "EXEC SP_COCKPIT_TAHSILAT @p0, @p1", startDate, endDate).ToListAsync();
            return rows.FirstOrDefault() ?? new TahsilatOzet();
        });
    }

    // ── SÖZLEŞME ──

    public async Task<List<SozlesmeRow>> GetSozlesmelerAsync(DateTime start, DateTime end)
    {
        var key = $"sp_soz_{start:yyyyMMdd}_{end:yyyyMMdd}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            db.Database.SetCommandTimeout(60);
            return await db.Database.SqlQueryRaw<SozlesmeRow>(
                "EXEC SP_COCKPIT_SOZLESME @p0, @p1", start, end).ToListAsync();
        });
    }

    // InvoiceStatusId Guid → Faturalanma durumu
    private static readonly Guid INVOICE_TAMAMLANDI = Guid.Parse("588a659c-2766-4872-880b-3bcf772439ba");
    private static readonly Guid INVOICE_KISMI = Guid.Parse("41a14f17-bd82-4927-a29e-592ab37f6bb0");
    private static readonly Guid INVOICE_FATURALANACAK = Guid.Parse("53056965-d3ec-4c71-b968-6493a898a7cc");

    public async Task<SozlesmeOzet> GetSozlesmeOzetAsync(DateTime start, DateTime end)
    {
        var rows = await GetSozlesmelerAsync(start, end);
        var yen = rows.Where(r => r.Yenilendi == 1).ToList();
        var bek = rows.Where(r => r.Yenilendi == 0).ToList();
        var archived = yen.Where(r => string.Equals(r.YeniStatus, "Archived", StringComparison.OrdinalIgnoreCase)).ToList();

        // Faturalanma durumu: yeni sözleşmenin InvoiceStatusId'sine bak (SP'den geliyor)
        var tamamlandiStr = INVOICE_TAMAMLANDI.ToString().ToUpperInvariant();
        var kismiStr = INVOICE_KISMI.ToString().ToUpperInvariant();
        var fatTamamlandi = yen.Where(r => string.Equals(r.YeniInvoiceStatusId?.Trim(), tamamlandiStr, StringComparison.OrdinalIgnoreCase)).ToList();
        var fatKismi = yen.Where(r => string.Equals(r.YeniInvoiceStatusId?.Trim(), kismiStr, StringComparison.OrdinalIgnoreCase)).ToList();

        // Fesih/İptal: yeni sözleşmenin ContractStatus = TerminationCancellation
        var fesih = yen.Where(r => string.Equals(r.YeniStatus, "TerminationCancellation", StringComparison.OrdinalIgnoreCase)).ToList();

        return new SozlesmeOzet
        {
            Toplam = rows.Count,
            YenilenenAdet = yen.Count,
            BekleyenAdet = bek.Count,
            EskiTutar = rows.Sum(r => r.EskiTutar ?? 0),
            YeniTutar = yen.Sum(r => r.YeniTutar ?? 0),
            BekleyenTutar = bek.Sum(r => r.EskiTutar ?? 0),
            ArchivedTutar = archived.Sum(r => r.YeniTutar ?? 0),
            ArchivedAdet = archived.Count,
            FaturalandiTutar = fatTamamlandi.Sum(r => r.YeniTutar ?? 0),
            FaturalandiAdet = fatTamamlandi.Count,
            KismiFaturalandiTutar = fatKismi.Sum(r => r.YeniTutar ?? 0),
            KismiFaturalandiAdet = fatKismi.Count,
            FesihTutar = fesih.Sum(r => r.YeniTutar ?? 0),
            FesihAdet = fesih.Count,
            FesihFirmalar = fesih.Select(r => r.Firma ?? "Bilinmeyen").ToList()
        };
    }

    // ── PIPELINE ──

    public async Task<PipelineResult> GetPipelineAsync(DateTime start, DateTime end, string? owner = null)
    {
        var ownerKey = owner ?? "all";
        var key = $"sp_pipe_{start:yyyyMMdd}_{end:yyyyMMdd}_{ownerKey}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            var rows = await db.Database.SqlQueryRaw<PipelineResult>(
                "EXEC SP_FIRSAT_PIPELINE_V2 @p0, @p1, @p2", start, end, owner == null ? DBNull.Value : owner).ToListAsync();
            return rows.FirstOrDefault() ?? new PipelineResult();
        });
    }

    // ── CACHE ──

    public void InvalidateAll()
    {
        lock (_keys) { foreach (var k in _keys) _cache.Remove(k); _keys.Clear(); }
    }

    private async Task<T> CachedAsync<T>(string key, Func<Task<T>> loader) where T : class
    {
        if (_cache.TryGetValue(key, out T? val) && val != null) return val;
        // Key-bazlı lock — farklı SP'ler parallel çalışır, aynı SP serialize olur
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out val) && val != null) return val;
            val = await loader();
            _cache.Set(key, val, TTL);
            lock (_keys) { _keys.Add(key); }
            return val;
        }
        finally { keyLock.Release(); }
    }
}
