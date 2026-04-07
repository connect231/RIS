using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SOS.Controllers;
using SOS.DbData;

namespace SOS.Services;

/// <summary>
/// Cache warmer'ın durumu — diğer servislerden / controller'dan okunabilir.
/// Singleton olarak register edilir.
/// </summary>
public class CockpitCacheWarmerState
{
    public DateTime? LastRefreshAt { get; set; }
    public int LastRefreshDurationMs { get; set; }
    public long RefreshCount; // Interlocked field — property değil
    public long FailureCount; // Interlocked field — property değil
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
}

/// <summary>
/// Arka planda CockpitController cache'ini hep sıcak tutar.
/// - Startup'tan 5 sn sonra ilk warm-up (DB migration tamamlansın diye)
/// - Sonra her 4 dk'da bir refresh (TTL 15 dk → her zaman buffer'lı)
/// - Uykuya giden kullanıcı cold path'e asla düşmez; ilk sayfa açılışı ~50ms
/// </summary>
public class CockpitCacheWarmer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CockpitCacheWarmer> _logger;
    private readonly CockpitCacheWarmerState _state;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(4);

    public CockpitCacheWarmer(
        IServiceScopeFactory scopeFactory,
        ILogger<CockpitCacheWarmer> logger,
        CockpitCacheWarmerState state)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MskDbContext>>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                // forceRefresh=true → cache bypass, DB'den taze data
                await CockpitController.LoadAllCachedDataAsync(contextFactory, cache, forceRefresh: true);

                var elapsed = DateTime.UtcNow - startedAt;
                _state.LastRefreshAt = DateTime.UtcNow;
                _state.LastRefreshDurationMs = (int)elapsed.TotalMilliseconds;
                _state.RefreshCount++;
                _logger.LogInformation("Cockpit cache refreshed in {ElapsedMs}ms (total refreshes: {Count})",
                    _state.LastRefreshDurationMs, _state.RefreshCount);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _state.FailureCount++;
                _state.LastError = ex.Message;
                _state.LastErrorAt = DateTime.UtcNow;
                _logger.LogError(ex, "Cockpit cache warmer refresh failed — will retry in {Interval}", RefreshInterval);
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
}
