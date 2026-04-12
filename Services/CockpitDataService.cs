using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
}

public class SozlesmeOzet
{
    public int Toplam { get; set; }
    public int YenilenenAdet { get; set; }
    public int BekleyenAdet { get; set; }
    public decimal EskiTutar { get; set; }
    /// <summary>Hedef: tüm yeni sözleşmelerin tutarı</summary>
    public decimal YeniTutar { get; set; }
    public decimal BekleyenTutar { get; set; }
    /// <summary>Gerçekleşen: sadece Archived yeni sözleşmelerin tutarı</summary>
    public decimal ArchivedTutar { get; set; }
    public int ArchivedAdet { get; set; }
}

#endregion

public interface ICockpitDataService
{
    Task<List<FaturaRow>> GetFaturalarAsync(DateTime start, DateTime end);
    Task<FaturaOzet> GetFaturaOzetAsync(DateTime start, DateTime end);
    Task<TahsilatOzet> GetTahsilatOzetAsync(DateTime start, DateTime end);
    Task<List<SozlesmeRow>> GetSozlesmelerAsync(DateTime start, DateTime end);
    Task<SozlesmeOzet> GetSozlesmeOzetAsync(DateTime start, DateTime end);
    void InvalidateAll();
}

public class CockpitDataService : ICockpitDataService
{
    private readonly IDbContextFactory<MskDbContext> _contextFactory;
    private readonly IMemoryCache _cache;
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly TimeSpan TTL = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> _keys = new();

    public CockpitDataService(IDbContextFactory<MskDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    // ── FATURA ──

    public async Task<List<FaturaRow>> GetFaturalarAsync(DateTime start, DateTime end)
    {
        var key = $"sp_fat_{start:yyyyMMdd}_{end:yyyyMMdd}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            return await db.Database.SqlQueryRaw<FaturaRow>(
                "EXEC SP_COCKPIT_FATURA @p0, @p1", start, end).ToListAsync();
        });
    }

    public async Task<FaturaOzet> GetFaturaOzetAsync(DateTime start, DateTime end)
    {
        var rows = await GetFaturalarAsync(start, end);
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
        var key = $"sp_tah_{start:yyyyMMdd}_{end:yyyyMMdd}";
        return await CachedAsync(key, async () =>
        {
            using var db = _contextFactory.CreateDbContext();
            // SP_COCKPIT_TAHSILAT aggregate döner (tek satır)
            var rows = await db.Database.SqlQueryRaw<TahsilatOzet>(
                "EXEC SP_COCKPIT_TAHSILAT @p0, @p1", start, end).ToListAsync();
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
            return await db.Database.SqlQueryRaw<SozlesmeRow>(
                "EXEC SP_COCKPIT_SOZLESME @p0, @p1", start, end).ToListAsync();
        });
    }

    public async Task<SozlesmeOzet> GetSozlesmeOzetAsync(DateTime start, DateTime end)
    {
        var rows = await GetSozlesmelerAsync(start, end);
        var yen = rows.Where(r => r.Yenilendi == 1).ToList();
        var bek = rows.Where(r => r.Yenilendi == 0).ToList();
        var archived = yen.Where(r => string.Equals(r.YeniStatus, "Archived", StringComparison.OrdinalIgnoreCase)).ToList();
        return new SozlesmeOzet
        {
            Toplam = rows.Count,
            YenilenenAdet = yen.Count,
            BekleyenAdet = bek.Count,
            EskiTutar = rows.Sum(r => r.EskiTutar ?? 0),
            YeniTutar = yen.Sum(r => r.YeniTutar ?? 0),         // hedef
            BekleyenTutar = bek.Sum(r => r.EskiTutar ?? 0),
            ArchivedTutar = archived.Sum(r => r.YeniTutar ?? 0), // gerçekleşen
            ArchivedAdet = archived.Count
        };
    }

    // ── CACHE ──

    public void InvalidateAll()
    {
        lock (_keys) { foreach (var k in _keys) _cache.Remove(k); _keys.Clear(); }
    }

    private async Task<T> CachedAsync<T>(string key, Func<Task<T>> loader) where T : class
    {
        if (_cache.TryGetValue(key, out T? val) && val != null) return val;
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out val) && val != null) return val;
            val = await loader();
            _cache.Set(key, val, TTL);
            lock (_keys) { _keys.Add(key); }
            return val;
        }
        finally { _lock.Release(); }
    }
}
