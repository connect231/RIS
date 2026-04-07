# SOS Agent Operasyonel Kuralları

Bu dosya `CLAUDE.md`'yi tamamlar. CLAUDE.md projenin **mimari** ve **iş mantığı** kurallarını anlatır; bu dosya **operasyonel guardrail'leri** (komutlar, "yapma" listeleri, agent seçim rehberi) içerir.

## Build / Run / Test Komutları

```bash
# Build
dotnet build SOS.csproj

# Çalıştır (default: http://localhost:5165)
dotnet run --project SOS.csproj

# Background'da çalıştır (agent kullanımı)
bg_shell start command="dotnet run --project SOS.csproj" type=server ready_port=5165 group=sos

# Sıfır warning hedefi — bu çıktıyı her PR öncesi temizle
dotnet build SOS.csproj /warnaserror
```

## Hangi Agent Ne İçin?

| Görev | Doğru Agent |
|---|---|
| CockpitController'a yeni endpoint eklemek | `dotnet-cockpit-engineer` |
| Tahsilat hesabı neden tutmuyor? | `finans-hesaplama-auditor` (önce) → düzeltme için `dotnet-cockpit-engineer` |
| Yeni metrik kartı tasarımı | `razor-ui-polisher` |
| Yavaş sorgu / N+1 sorunu | `sql-ef-query-pro` |
| Yeni tablo / kolon eklemek | `sql-ef-query-pro` (raw SQL DatabaseMigrationService'e ekler) |
| Genel araştırma / dosya keşfi | `scout` |
| Bağımsız küçük iş | `worker` |

## Pazarlıksız Yasaklar

1. **EF Migration ekleme.** `dotnet ef migrations add` çalıştırma. Şema değişikliği `Services/DatabaseMigrationService.cs` içine raw SQL.
2. **Hardcoded finansal sabit ekleme.** Aylık hedef, ürün listesi, eşleştirme — hepsi DB'den (`TBLSOS_*`).
3. **`AYLIK_HEDEF` constant** veya benzeri hardcoded para değeri kodda görünmez.
4. **CockpitController'a `MskDbContext` direkt enjekte etme.** Her zaman `IDbContextFactory<MskDbContext>` + `using var db = factory.CreateDbContext()`.
5. **DEV mode auto-login'i kaldırma.** `AccountController.Login GET` içindeki otomatik giriş bilinçli — sadece `// DEV:` yorumunu koru.
6. **İade/Ret faturaları tahsilat hesabına dahil etme.**
7. **Tahsilat tarih hesaplarında sadece `Fatura_Vade_Tarihi` kullan.** Ödeme sözü mantığı projeden tamamen kaldırıldı.
8. **"Diğer" / "Eşleşmemiş" kategorisi ürün kırılımına ekleme.** Varuna'da eşleşmeyen fatura ürün dağılımına girmez.
9. **Türkçe label'lara İngilizce karıştırma.** "Q1" → "1. Çeyrek".
10. **Kuruş gösterme.** `N0` format, `₺` prefix.
11. **jQuery / React / Vue / Alpine ekleme.** Vanilla JS.
12. **Sayfa reload ile filtre değiştirme.** AJAX zorunlu.

## Doğrulama Zorunlulukları

Herhangi bir kod değişikliği sonrası:

1. ✅ `dotnet build SOS.csproj` — sıfır error, sıfır yeni warning
2. ✅ `lsp diagnostics` — değişen tüm dosyalar temiz
3. ✅ Finansal hesap değiştiyse → `finans-hesaplama-auditor` agent'ı çağır VE Excel referans dosya ile karşılaştırma raporu iste
4. ✅ UI değiştiyse → `bg_shell` ile uygulamayı çalıştır + `browser_screenshot` ile görsel doğrula

## Cache Davranışı

- Cache TTL **5 dakika**
- Yeni cache key sabiti `CockpitController` üst bloğundaki listeye eklenmeli
- `SemaphoreSlim _cacheLock` ile sarılmalı
- Yeni tablo → yeni cache key → preload pattern

## Excel Referans

`Excel /Satış Analizi Kaynak Liste (Ham veri) (2023-2026).Rev (1).xlsx` — bu dosya **doğruluk kaynağıdır**.

- Q1 2026 toplam: ₺102.3M (389 sipariş, 698 kalem)
- Yıllık hedef: ₺600M

Dashboard'da gösterilen değer bu dosya ile **birebir** tutmalı. ₺1'in üzerinde sapma BUG'dır.

## Türkçe Locale Hatırlatması

```csharp
// Doğru
var fmt = new System.Globalization.CultureInfo("tr-TR");
amount.ToString("C0", fmt)  // ₺1.234.567

// Yanlış
amount.ToString("C0")  // OS locale'ine bağlı
```

```js
// Doğru
new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }).format(value)

// Yanlış
'$' + value.toFixed(2)
```

## Branch / Commit

- Türkçe commit mesajı kabul (proje stili)
- `CHANGELOG.md` UI ve mimari değişikliklerde güncellenir (tarih başlığıyla)
- `CODE_REVIEW_REPORT.md` ve `Univera_Connect_Portal_Proje_Dokumani.md` referans dokümanlardır — silmeyin, ihtiyaç olursa güncelleyin
