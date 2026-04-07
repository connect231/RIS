# Teknik Borç / İleri Görevler

Kapsamlı denetim (7 Nisan 2026) sonrası ertelenen konular.

## Production Sertleştirmesi

- [ ] **Tailwind CDN → static build**
  - `_Layout.cshtml` ve `_LayoutMusteri.cshtml`'den `<script src="https://cdn.tailwindcss.com">` kaldır
  - `<link rel="stylesheet" href="~/css/site.css">` ekle
  - `tailwind.config.js` `content` ayarında tüm Razor view'lar ve JS inline string'lerin kapsandığını doğrula
  - Build pipeline'a `npm run build:css` ekle (deploy öncesi)

- [ ] **CSP sıkılaştırma (production)**
  - Tailwind CDN kaldırıldıktan sonra `script-src` güncellenebilir
  - `'unsafe-eval'` kaldır
  - `'unsafe-inline'` script → nonce-based yaklaşım (tüm inline `<script>`'lere nonce ekle)
  - `'unsafe-inline'` style → PostCSS ile class'lara yönlendir

- [ ] **Git history'den sızan şifreleri temizle**
  - `git filter-repo --invert-paths --path appsettings.Development.json --path appsettings.Production.json`
  - VEYA BFG: `bfg --delete-files appsettings.Development.json`
  - Sonrasında force-push + tüm clone'ları yeniden clone
  - **DB ve SMTP şifrelerini ROTATE ET** (sızan kullanımda olmayacak)

## Mimari

- [ ] **CockpitController refactor** (1592 satır → 3+ servis)
  - `CockpitDataService` — `LoadAllCachedDataAsync`, dedupe, mapping
  - `CockpitMetricsCalculator` — `ComputeMetrics` ve finansal hesaplar
  - `CockpitController` — yalın action'lar
  - **Önce unit test coverage oluştur**, sonra refactor

- [ ] **FirsatAnalizController refactor** (1907 satır)
  - Aynı pattern: service extraction
  - Ortak cache pattern için `ICacheService` abstraction

## Observability

- [ ] Structured logging (Serilog veya built-in ILogger structured)
- [ ] Request tracing (AspNetCore correlation ID)
- [ ] Cache hit/miss metriği (`CockpitCacheWarmerState` zaten var — histogram ekle)
- [ ] Error tracking (Sentry veya ElasticAPM)

## Kod Kalitesi

- [ ] `UrlEncryptionService` hardcoded key → user-secrets
- [ ] `ParametreController` dışında tüm controller'larda `IDbContextFactory` pattern (denetim tamamlandı)
- [ ] Unit test + integration test seti
- [ ] FirsatAnaliz cache key'leri ile Cockpit cache key'leri ortak namespace
