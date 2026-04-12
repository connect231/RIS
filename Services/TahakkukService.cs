using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SOS.DbData;

namespace SOS.Services;

/// <summary>
/// Tahakkuk override map'ini cache'leyen ve invalidate eden ortak servis.
/// Hem CockpitController hem FirsatAnalizController bu map'i kullanır.
/// </summary>
public interface ITahakkukService
{
    /// <summary>
    /// FaturaNo → TahakkukTarihi map'i. Yalnızca Aktif=true kayıtlar.
    /// Cache'lenir; CommitInvalidate() çağrıldığında invalidate olur.
    /// </summary>
    Task<Dictionary<string, DateTime>> GetTahakkukMapAsync();

    /// <summary>
    /// Tahakkuk değişikliği sonrası cache'i temizler — sonraki çağrı DB'den çeker.
    /// Cockpit veri cache'ini de invalidate eder ki tahakkuk hemen yansısın.
    /// </summary>
    void Invalidate();
}

public class TahakkukService : ITahakkukService
{
    private const string CACHE_KEY = "tahakkuk_map_v1";
    private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(15);

    private readonly IDbContextFactory<MskDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    public TahakkukService(IDbContextFactory<MskDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public async Task<Dictionary<string, DateTime>> GetTahakkukMapAsync()
    {
        if (_cache.TryGetValue(CACHE_KEY, out Dictionary<string, DateTime>? cached) && cached != null)
            return cached;

        using var db = _contextFactory.CreateDbContext();
        var records = await db.TBLSOS_FATURA_TAHAKKUKs.AsNoTracking()
            .Where(t => t.Aktif)
            .Select(t => new { t.SapReferansNo, t.FaturaNo, t.TahakkukTarihi })
            .ToListAsync();

        var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            map[r.SapReferansNo] = r.TahakkukTarihi;       // SAP key (primary)
            if (!string.IsNullOrEmpty(r.FaturaNo))
                map[r.FaturaNo] = r.TahakkukTarihi;          // FaturaNo key (compat)
        }

        _cache.Set(CACHE_KEY, map, CacheTTL);
        return map;
    }

    public void Invalidate()
    {
        _cache.Remove(CACHE_KEY);
        // Cockpit cache'lerini de temizle ki yeni tahakkuk anında yansısın
        _cache.Remove("cockpit_faturalar");
        _cache.Remove("cockpit_sozlesmeler");
        _cache.Remove("cockpit_urun_map");
        _cache.Remove("cockpit_musteri_map");
        _cache.Remove("cockpit_hedefler");
        _cache.Remove("cockpit_varuna_tutar");
        _cache.Remove("cockpit_urun_grup_map");
    }
}
