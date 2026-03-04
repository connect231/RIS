# 🔬 Univera Connect — Kapsamlı Kod İnceleme Raporu

> **Tarih:** 17 Şubat 2026  
> **İncelenen Proje:** UniveraCP (ASP.NET Core 9.0)  
> **Perspektif:** Senior Software Architect / Clean Code Expert

---

## 🏥 Genel Sağlık Puanı: **4/10**

```
Kod Kalitesi & Standartlar  ████░░░░░░  3/10
Mimari Tutarlılık           ███░░░░░░░  3/10
AI Yan Etkileri             █████░░░░░  5/10
Performans & Güvenlik       ████░░░░░░  4/10
Bakılabilirlik              ████░░░░░░  4/10
```

> [!CAUTION]
> Bu puan "korkunç" anlamına gelmiyor. Proje çalışıyor, özellikler deliver ediliyor. Ancak **teknik borç birikimi kritik seviyeye yaklaşıyor** ve şu an müdahale edilmezse 3-6 ay içinde "yeni özellik ekleme maliyeti → mevcut bug sayısı" eğrisi tehlikeli bir hal alacaktır.

---

## 1. 📐 Kod Kalitesi ve Standartlar (3/10)

### 🔴 KRİTİK: God Method — `UniveraHomeController.Index` (1008 satır)

Bu metod tek başına projenin en büyük teknik borç kaynağıdır. Metod şunları yapıyor:

1. Kullanıcı doğrulama ve yetkilendirme
2. Cookie yönetimi
3. ViewBag assignment (20+ property)
4. 5 farklı stored procedure çağrısı (batch + parallel)
5. Ticket stats aggregation
6. SLA hesaplama
7. TFS request filtering
8. Budget approval hesaplama
9. Finance chart data preparation
10. License expiry kontrol
11. Debug data oluşturma

```csharp
// UniveraHomeController.cs:43-1051
public async Task<IActionResult> Index(int? filteredCompanyId = null)
{
    // ... 1008 SATIR TEK METOD İÇİNDE ...
    return View();
}
```

> [!IMPORTANT]
> **SOLID — Single Responsibility İhlali:** Bu metod en az 6-7 farklı sorumluluk taşıyor. Her biri ayrı bir servis sınıfına taşınmalıdır.

### 🔴 DRY İhlalleri — Cookie Handling 4+ Yerde Tekrarlanıyor

Aynı cookie yönetimi mantığı dört farklı controller'da birebir tekrarlanmış:

```csharp
// UniveraHomeController.cs:67-80
// TaleplerController.cs:80-91
// FinansController.cs:62-73
// RaporlarController.cs (benzer pattern)

if (filteredCompanyId.HasValue) {
    if (filteredCompanyId.Value == -1) {
        Response.Cookies.Delete("SelectedCompanyId");
    } else if (...) {
        Response.Cookies.Append("SelectedCompanyId", ...);
    }
}
```

**Refactor:** Bu mantık `CompanyResolutionService` içine taşınmalı veya bir `BaseController` oluşturulmalıdır.

### 🟡 ViewBag Aşırı Kullanımı — Compile-Time Güvenliği Yok

`UniveraHomeController.Index` metodunda **20+ ViewBag property** set ediliyor. ViewBag compile-time type safety sağlamaz, runtime'da `NullReferenceException` riski taşır.

```csharp
// UniveraHomeController.cs — 20+ farklı yerde
ViewBag.OpenTicketsCount = ...;
ViewBag.CriticalCount = ...;
ViewBag.OpenDevRequestsCount = ...;
ViewBag.CompletedDevRequestsCount = ...;
ViewBag.UatTestCount = ...;
ViewBag.BudgetApprovalStats = ...;
ViewBag.SupportRequestStats = ...;
ViewBag.PendingBudgetEffort = ...;
ViewBag.PendingBudgetCost = ...;
ViewBag.FinanceChartData = ...;
ViewBag.DebugOrders = ...;
ViewBag.DebugExceptions = ...;
// ... devam ediyor
```

**Refactor:** Strongly-typed `UniveraHomeViewModel` oluşturun.

### 🟡 Auto-Migration SQL HTTP Handler İçinde

Her HTTP isteğinde (!) DDL ALTER TABLE çalıştırılıyor:

```csharp
// TaleplerController.cs:34-60
public async Task<IActionResult> Index(...) {
    try {
        _mskDb.Database.ExecuteSqlRaw(
            "IF NOT EXISTS(...) ALTER TABLE TBL_TALEP ADD TXT_PO VARCHAR(50)...");
        _mskDb.Database.ExecuteSqlRaw(
            "IF NOT EXISTS(...) ALTER TABLE TBL_TALEP ADD TRHKAYIT DATETIME...");
        // ... 4 adet ALTER TABLE komutu daha
    } catch { /* Ignore permissions/errors */ }
```

> [!WARNING]
> **Bu, her Index çağrısında database metadata sorgusu çalıştırıyor.** Performans kaybı + güvenlik riski. Migration'lar EF Core Migrations veya startup'ta çalıştırılmalı, asla request handler içinde olmamalı.

---

## 2. 🏗️ Mimari Tutarlılık / Spagetti Kontrolü (3/10)

### 🔴 God Objects: Controller Boyutları

| Controller | Satır | En Büyük Metod | Metod Satırı |
|---|---|---|---|
| [UniveraHomeController](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/Controllers/Musteri/UniveraHomeController.cs) | 1081 | `Index` | **1008** |
| [TaleplerController](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/Controllers/TaleplerController.cs) | 1032 | `Index` | 425 |
| [FinansController](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/Controllers/FinansController.cs) | 927 | `Index` | 275 |
| [AccountController](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/Controllers/KullaniciControlers/AccountController.cs) | 927 | `Login` | 172 |
| [RaporlarController](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/Controllers/RaporlarController.cs) | 774 | `GetDevelopmentRequestsAsync` | 231 |

> İdeal: Bir controller metodu **50-80 satır** olmalıdır. `Index` metodu 1008 satır = **12-20x** ideal boyutun üstünde.

### 🔴 Katman Sızıntısı: Controller İçinde Business Logic

Controller'lar doğrudan stored procedure çağrıyor, veri dönüştürme yapıyor ve iş mantığı çalıştırıyor. Service layer neredeyse yok.

```
┌─────────────────────────────────────────┐
│ Mevcut Mimari (Spagetti)                │
│                                         │
│ Controller → DbContext → SP → ViewBag   │
│    ↕ (Cookie yönetimi)                  │
│    ↕ (Paralel data fetching)            │
│    ↕ (Aggregation & chart data)         │
│    ↕ (View return)                      │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│ Olması Gereken Mimari                   │
│                                         │
│ Controller → Service → Repository → SP  │
│      ↓           ↓                      │
│  ViewModel    Business Logic            │
│      ↓                                  │
│     View                                │
└─────────────────────────────────────────┘
```

### 🟡 CompanyResolutionService İç Tekrar

`Admin` ve `UniveraInternal` blokları neredeyse birebir aynı (satır 55-103):

```csharp
// CompanyResolutionService.cs:55-78 (Admin)
if (kullanici.LNGKULLANICITIPI == (int)UserType.Admin) {
    var allProjects = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
        .AsNoTracking().ToListAsync();
    result.AuthorizedCompanies = allProjects;
    if (filteredCompanyId.HasValue && filteredCompanyId.Value > 0) {
        result.TargetCompanyIds = new List<int> { filteredCompanyId.Value };
    } else {
        result.TargetCompanyIds = allProjects.Select(p => p.LNGKOD).ToList();
    }
}

// CompanyResolutionService.cs:80-103 (UniveraInternal)
// ... AYNI KOD BİREBİR TEKRARLANMIŞ
```

---

## 3. 🤖 AI Yan Etkileri (5/10)

### 🟡 Yorum Çöplüğü — Kaldırılmamış Debug/TODO Yorumları

AI destekli geliştirmenin tipik izi: birden fazla "TODO", "NOTE", "DEBUG" ve tekrarlanan yorumlar:

```csharp
// UniveraHomeController.cs:129-130 — Aynı yorum iki kez
// Execute Batch SPs Sequentially (DbContext is not thread-safe)
// Execute Batch SPs Sequentially (DbContext is not thread-safe)

// UniveraHomeController.cs:139-140 — Yine aynı yorum iki kez
// Populate Company Map
// Populate Company Map

// UniveraHomeController.cs:596-608 — AI "düşünme süreci" yorumlara sızmış
// We will fetch SP_N4B_TICKETLARI here directly parallelized...
// We will fetch SP_N4B_TICKETLARI here directly parallelized...
```

### 🟡 Gereksiz Jenerik Hata Yönetimi — Sessiz Yakalama

AI'ın en yaygın kötü alışkanlığı: boş `catch` blokları. İstisna yutulup problemi görünmez kılıyor:

```csharp
// TaleplerController.cs:60
} catch { /* Ignore permissions/errors */ }

// UniveraHomeController.cs:246
} catch {}

// UniveraHomeController.cs:252
} catch {}

// UniveraHomeController.cs:258
} catch {}

// UniveraHomeController.cs:264
} catch {}

// UniveraHomeController.cs:270
} catch {}

// UniveraHomeController.cs:655
} catch {}
```

> [!WARNING]
> **10+ boş catch bloğu:** Bu, prodüksiyonda hatalar oluştuğunda sizi "veri eksik ama neden bilmiyoruz" durumuna düşürür. En azından `ILogger.LogWarning()` çağrısı yapılmalıdır.

### 🟡 Hallucinated/Tutarsız Parametre Kullanımı

```csharp
// UniveraHomeController.cs:238
int companyId = Convert.ToInt16(cid); // ⚠️ Int16 max = 32767
                                       // Company ID > 32767 ise OverflowException!
```

`Convert.ToInt16` kullanımı gereksiz ve tehlikeli. `cid` zaten `int` türünde.

### 🟢 Pozitif: Enum Kullanımı

`UserType` enum'ı doğru şekilde oluşturulmuş ve `CompanyResolutionService`'te kullanılıyor:

```csharp
// CompanyResolutionService.cs:55
if (kullanici.LNGKULLANICITIPI == (int)UserType.Admin)
```

---

## 4. 🔒 Performans ve Güvenlik (4/10)

### 🔴 KRİTİK GÜVENLİK: Plaintext Credentials

[appsettings.json](file:///c:/Users/univera/.gemini/antigravity/scratch/UniveraCP/appsettings.json) dosyasında **düz metin şifreler** var:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "...User Id=UNIVERA;Password=P@ssw0rd;..."
  },
  "Email": {
    "Password": "liruijmmxedfzdeb"   // ← SMTP App Password
  },
  "Zabbix": {
    "Username": "univera_dashboard",
    "Password": ""                    // ← Boş olsa bile yapı yanlış
  }
}
```

> [!CAUTION]
> **Bu dosya git'e commit edilmiş!** DB şifresi, SMTP app password'ü kaynak kodda. `Azure Key Vault`, `User Secrets`, veya environment variables kullanılmalı. **Şifreleri hemen değiştirin.**

### 🔴 Zayıf Password Policy

```csharp
// Program.cs:46-50
options.Password.RequiredLength = 7;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequireUppercase = false;
options.Password.RequireLowercase = false;
options.Password.RequireDigit = false;
```

7 karakter, karmaşıklık yok = "aaaaaaa" geçerli bir şifre.

### 🔴 CSP Header'da unsafe-inline + unsafe-eval

```csharp
// Program.cs:92
"script-src 'self' 'unsafe-inline' 'unsafe-eval' ..."
```

`unsafe-inline` ve `unsafe-eval` birlikte XSS korumasını neredeyse tamamen devre dışı bırakır.

### 🟡 Potansiyel N+1 ve Aşırı Parallelizm

```csharp
// UniveraHomeController.cs:615
await Parallel.ForEachAsync(companiesToFetch,
    new ParallelOptions { MaxDegreeOfParallelism = 10 }, ...)
```

10 firma × 5 SP = **50 eşzamanlı DB bağlantısı potansiyeli.** Connection pool tükenme riski. Batch SP'ler zaten var ama tüm kullanıcı tipleri için kullanılmıyor.

### 🟡 Debug Dosyaları Repo'da

Proje kökünde **13 debug dosyası** var:

```
debug_batch_tfs.txt (2.9 KB)
debug_filter_log.txt (29 KB)
debug_finans_data.txt (9.5 KB)
debug_tfs.txt (241 KB!)
zabbix_keys_dump.txt (49 KB)
build_error.txt (73 KB)
...
```

> [!WARNING]
> 241 KB'lık debug dump'ı repository'de durmamalı. `.gitignore`'a eklenmeli ve commit'ten kaldırılmalıdır.

### 🟡 ZabbixService State Management Riski

```csharp
// ZabbixService.cs:15-16
private string? _authToken;
private bool _isAuthenticated = false;
```

`ZabbixService` `HttpClient` üzerinden registered ve muhtemelen singleton-like davranıyor. `_authToken` instance state'i, token expire olunca refresh mekanizması yok.

---

## 5. 🔧 Bakılabilirlik (4/10)

### 🔴 Onboarding Maliyeti: ÇOK YÜKSEK

6 ay sonra yeni bir geliştirici bu kodu eline aldığında:

| Metrik | Değer | İdeal |
|---|---|---|
| En büyük metod | 1008 satır | ≤ 50 satır |
| Controller başına ortalama satır | ~950 | ≤ 300 |
| ViewBag property sayısı | 20+ | 0 (ViewModel) |
| Magic string sayısı | 30+ | 0 (Constants) |
| Boş catch bloğu | 10+ | 0 |
| Debug dosya sayısı (repo'da) | 13 | 0 |

### 🟡 Hardcoded Magic Numbers ve Strings

```csharp
// UniveraHomeController.cs:105
DateTime trh = new DateTime(2025, 1, 1);       // Magic date

// UniveraHomeController.cs:580
EstimatedCost = x.Value.Effort * 22500          // Magic number: Birim maliyet?

// UniveraHomeController.cs:133
var batchTickets = await _mskDb.SP_N4B_TICKETLARI_COKLU_FILTREAsync(
    companyCodes, email, 3);                     // 3 = ??? Open?

// CompanyResolutionService.cs:177
if (filteredCompanyId.HasValue && 
    authorizedCompanyIds.Contains(filteredCompanyId.Value))  // -1 kontrolü yok!
```

### 🟡 Eksik Input Validation

`int.Parse` doğrudan kullanılıyor — kötü niyetli input crash verebilir:

```csharp
// Birden fazla controller'da tekrarlanan pattern:
int userId = int.Parse(userIdStr);  // FormatException riski
```

### 🟢 Pozitif Noktalar

1. **CompanyResolutionService** — Doğru yönde atılmış iyi bir adım
2. **UserType Enum** — Magic number yerine enum kullanımı
3. **AsNoTracking()** — READ sorgularında doğru kullanım
4. **Parallel.ForEachAsync** — Modern async pattern (doğru kullanıldığında)
5. **Response Compression** — Performance optimization mevcut

---

## 📋 Öncelikli Refactoring Yol Haritası

### Sprint 1: KRİTİK GÜVENLİK (1-2 Gün)

| # | Aksiyon | Dosya | Etki |
|---|---|---|---|
| 1 | Credentials'ı `User Secrets` / `Environment Variables`'a taşı | `appsettings.json` | 🔴 Güvenlik |
| 2 | Git geçmişinden credential'ları temizle | Repo | 🔴 Güvenlik |
| 3 | Debug dosyalarını `.gitignore`'a ekle | Repo root | 🟡 Temizlik |
| 4 | Password policy'yi güçlendir | `Program.cs` | 🔴 Güvenlik |

### Sprint 2: TEMELİ DÜZELT (1 Hafta)

| # | Aksiyon | Dosya | Etki |
|---|---|---|---|
| 5 | Cookie handling'i `CompanyResolutionService`'e taşı | 4 Controller | 🔴 DRY |
| 6 | Boş catch bloklarına loglama ekle | Tüm Controllers | 🟡 Debug |
| 7 | `Convert.ToInt16` → `cid` (zaten int) | UniveraHome, Talepler | 🟡 Bug Risk |
| 8 | Auto-migration SQL'i `Startup`'a / `EF Migration`'a taşı | TaleplerController | 🟡 Performans |

### Sprint 3: MİMARİ İYİLEŞTİRME (2-3 Hafta)

| # | Aksiyon | Etki |
|---|---|---|
| 9 | `UniveraHomeController.Index`'i 7+ servise parçala: `DashboardService`, `TicketService`, `FinanceService`, `TfsService`, `SlaService`, `LicenseService`, `BudgetService` | 🔴 Mimari |
| 10 | ViewBag → Strongly-typed ViewModel | 🟡 Type Safety |
| 11 | Magic strings → Constants class | 🟡 Bakılabilirlik |
| 12 | `CompanyResolutionService` içindeki Admin/UniveraInternal duplikasyonunu birleştir | 🟡 DRY |

---

## 🎯 Sonuç

Bu proje **"çalışan ama kırılgan" aşamasında.** Mevcut haliyle özellikler deliver ediliyor, ancak:

- Her yeni özellik ekleme maliyeti artıyor (1008 satırlık bir metoda dokunmak = yüksek risk)
- Bug fix'lerde yan etkiler kaçınılmaz (cookie logic 4 yerde tekrarlanıyor)
- Güvenlik açıkları mevcut (plaintext credentials, zayıf password policy)

**Öneri:** Sprint 1'i (güvenlik) hemen, Sprint 2'yi bu hafta, Sprint 3'ü gelecek sprintte planlayın. Her sprint sonunda tekrar review yapılmalı.
