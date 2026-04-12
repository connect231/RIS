# SOS — Sales Operating System

Finansal cockpit dashboard uygulaması. ASP.NET Core MVC (.NET 10), Razor Views, Tailwind CSS, SQL Server.

## Proje Amacı
Satış ekibinin fatura, tahsilat, sözleşme ve hedef takibini tek ekrandan yapabilmesi. Apple-kalitesinde 60fps UI.

## Teknoloji
- **Backend:** ASP.NET Core MVC, Entity Framework Core, IDbContextFactory (thread-safety), IMemoryCache (5 dk TTL), SemaphoreSlim
- **Frontend:** Razor Views, Tailwind CDN, vanilla JS (no React/Vue), requestAnimationFrame animasyonlar
- **DB:** SQL Server `10.135.140.17\yazdes` / `UNIVERA_CUSTOMER_PORTAL`
- **Target:** net10.0

## Stored Procedure Mimarisi

### Genel Prensip
Dashboard kartları (fatura, tahsilat, sözleşme) **SP'lerden** besleniyor. SP'ler tek kaynak — hem Cockpit hem FırsatAnaliz aynı `ICockpitDataService` üzerinden SP çağırır. Bir SP değişince tüm ekranlar etkilenir.

### SP'ler
- **SP_COCKPIT_FATURA(@Start, @End):** VIEW ∩ Varuna(Closed) - İade/Ret + Tahakkuk sentetik. Satır bazlı döner (FaturaNo, EfektifTarih, NetTutar, Firma). Ürün dağılımı ve detay tablosu da bu SP'nin FaturaNo listesinden hesaplanır.
- **SP_COCKPIT_TAHSILAT(@Start, @End):** VIEW bazlı, İade/Ret hariç, Hukuki takip hariç. Aggregate döner (TahsilEdilen, BekleyenBakiye, VadesiGelen). Tahsil_Tarihi bazlı PAY, Fatura_Vade_Tarihi bazlı PAYDA.
- **SP_COCKPIT_SOZLESME(@Start, @End):** FinishDate+1 = yenileme ayı. Yeni sözleşme eski.Id'yi `RelatedContractId` ile referans gösteriyor (ters bağlantı, OUTER APPLY). Hedef = yeni sözleşme tutarı, Gerçekleşen = Archived olanlar.

### ICockpitDataService
```
Services/CockpitDataService.cs
  GetFaturalarAsync(start, end)      → List<FaturaRow> (satır bazlı)
  GetFaturaOzetAsync(start, end)     → FaturaOzet (toplam/adet)
  GetTahsilatOzetAsync(start, end)   → TahsilatOzet (aggregate)
  GetSozlesmelerAsync(start, end)    → List<SozlesmeRow> (satır bazlı)
  GetSozlesmeOzetAsync(start, end)   → SozlesmeOzet (yenilenen/bekleyen)
  InvalidateAll()                    → tüm SP cache temizle
```
- 5dk TTL cache, SemaphoreSlim double-check lock
- Cache key: `sp_fat_20260301_20260331` formatı (tarih bazlı)
- CacheWarmer startup'ta sabit SP'leri preload eder (haftalık, aylık, YTD)

### SP Çağrı Akışı (filtre değişiminde)
```
Kullanıcı filtre değiştirir → AJAX
  → 3 dönem SP parallel: Fatura + Tahsilat + Sözleşme (filtre tarihiyle)
  → 6 sabit SP cache'den: Nisan, YTD, Geçen Hafta, Bu Hafta, Aylık, Yıllık
  → LoadAllCachedDataAsync: ürün kırılımı, müşteri eşleşme (eski cache)
```

### İade/Ret Kuralı
- İade/Ret faturalar **tamamen atlanır** (ne pozitif ne negatif — `continue`)
- Excel verisinde iade zaten net satış olarak düşülmüş, Varuna'da orijinal pozitif
- VIEW'de İade/Ret + Varuna eşleşen → `varunaTutarMap`'ten blacklist ile çıkarılır
- Sentetik faturalar: sadece tahakkuklu olanlar eklenir (tahakkuksuz → VIEW'e girinceye kadar beklenir)

### Tahakkuk (SAP Bazlı)
- **Primary key: SapReferansNo** (Varuna `SAPOutReferenceCode`)
- FaturaNo (matbu no) opsiyonel — sonradan atanabilir
- `TahakkukService`: dual-key map (SAP + FaturaNo compat)
- BulkImport: SAP bazlı primary eşleşme, SipID prefix fallback
- UI: SAP no veya fatura no ile arama

### Hukuki Takip
- `Hukuki_Durum` kolonu dolu olan faturalar tahsilat PAYDA'sından hariç
- SP_COCKPIT_TAHSILAT: `ISNULL(LTRIM(RTRIM(Hukuki_Durum)), '') = ''` filtresi

## Kritik Mimari Kararlar

### DbContext Pattern
`AddDbContext` + `AddDbContextFactory(ServiceLifetime.Scoped)` birlikte kullanılıyor.
- `AddDbContext` → ClaimsFactory, LogService gibi scoped servisler için
- `IDbContextFactory` → CockpitController'da `using var db = _contextFactory.CreateDbContext()` ile bağımsız context

### Tahsilat Tarih Mantığı
- Fatura kartı → `EfektifFaturaTarihi` bazlı (tahakkuk varsa o, yoksa `Fatura_Tarihi`)
- Tahsilat kartı → `Fatura_Vade_Tarihi` bazlı
- Vadesi geçmiş → `Fatura_Vade_Tarihi` < dönem başı VE bakiye > 0
- İade/Ret durumlu faturalar tahsilat hesaplarından HARİÇ
- **NOT:** Ödeme sözü tarihi mantığı projeden tamamen kaldırıldı — sadece `Fatura_Vade_Tarihi` kullanılır.

### Tahakkuk Sistemi (SAP Bazlı Tarih Override)
- **Tablo:** `TBLSOS_FATURA_TAHAKKUK` — `SapReferansNo` (primary key), `FaturaNo` (opsiyonel), `TahakkukTarihi`, `Aktif`
- **Amaç:** Bir faturanın muhasebe dönemi, fatura kesilme tarihinden farklı olabilir. Tahakkuk kaydı fatura tarihini override eder.
- **SAP bazlı:** Varuna `SAPOutReferenceCode` ile eşleşir. MatbuNo (SerialNumber) sonradan atanabilir.
- **Servis:** `TahakkukService.GetTahakkukMapAsync()` → `FaturaNo → TahakkukTarihi` map (15 dk cache)
- **Akış:** `LoadAllCachedDataAsync` her fatura için: tahakkuk varsa → `EfektifFaturaTarihi = TahakkukTarihi`, yoksa → `EfektifFaturaTarihi = Fatura_Tarihi`
- **Tüm raporlama** (fatura kartı, tahsilat, CEI, YTD, Fırsat Analiz) `EfektifFaturaTarihi` kullanır
- `Invalidate()` çağrıldığında tüm Cockpit cache'i de temizlenir — anında yansır

### Varuna Fallback (Excel'de Olmayan Faturalar)
- Excel'e (`VIEW_CP_EXCEL_FATURA`) bazen geç girilir. Bu arada Varuna'da Closed sipariş zaten mevcut olabilir.
- `LoadAllCachedDataAsync` Varuna'da `Closed` + `TotalNetAmount > 0` olup Excel'de `Fatura_No` karşılığı **olmayan** siparişleri **sentetik fatura** olarak ekler.
- Sentetik kayıt: `NetTutar = TotalNetAmount`, `EfektifFaturaTarihi = InvoiceDate` (veya tahakkuk override), `VarunaEslesti = true`, `Durum = null`
- Excel'e girildiğinde otomatik olarak gerçek kayıtla değişir (SerialNumber eşleşmesi ile deduplicate)
- Hem Cockpit hem Fırsat Analiz aynı cached veriyi kullandığı için her iki ekranda da görünür

### Hedef Sistemi (DB bazlı)
- `TBLSOS_HEDEF_AYLIK` → aylık hedefler (ay bazlı farklı tutarlar, toplam ₺600M/yıl)
- `TBLSOS_ANA_URUN` → 8 ana ürün kategorisi (Enroute, Stokbar, Quest, ServiceCore, Varuna, Hosting, E-Dönüşüm, BFG)
- `TBLSOS_URUN_ESLESTIRME` → StockCode → Ana ürün eşleşmesi (~145 kayıt, Excel onaylı; DB'de nadiren duplicate StokKodu olabilir → GroupBy+First ile temizlenir)
- Hardcoded `AYLIK_HEDEF` YOK — tümü DB'den

### Ürün Eşleşme Zinciri
```
Fatura.Fatura_No → TBL_VARUNA_SIPARIS.SerialNumber → .OrderId
  → TBL_VARUNA_SIPARIS_URUNLERI.CrmOrderId → .StockCode
  → TBLSOS_URUN_ESLESTIRME → TBLSOS_ANA_URUN
```
Eşleşme satır bazlı (mask bazlı değil) — aynı mask farklı ana ürüne gidebilir.

### Tahsilat Kartı Gösterimi
- **Büyük tutar** = `SUM(Fatura_Toplam)` — dönemdeki toplam
- **Tahsil edilen** = `SUM(Tahsil_Edilen)`
- **Kalan** = `SUM(Bekleyen_Bakiye)`

### Filtreler
- Pill-nav: Bu ay, Geçen ay, 1-4. Çeyrek, YTD
- Dinamik tarih: başlangıç/bitiş date picker + Uygula butonu
- Bu ay/çeyrekler tam dönem (bugüne kısıtlanmaz)
- AJAX ile filtre değişiminde tüm kartlar güncellenir (sayfa reload yok)

## DEV Mode
- Login şifresiz — `AccountController.Login GET` ilk kullanıcıyı otomatik giriş yapar
- Production'da `PasswordCheck` yeniden aktif edilmeli

## Migration Sistemi
`DatabaseMigrationService.cs` — raw SQL ile IF NOT EXISTS pattern. EF Migration kullanılmıyor.
Yeni tablo eklerken buraya eklenir, uygulama başlangıcında otomatik çalışır.

## Türkçe UI Kuralları
- "Q2" yerine "2. Çeyrek"
- İlk harf büyük, hepsi büyük OLMAZ
- Tarihler dd.MM.yyyy formatı
- Para birimi: ₺ prefix, N0 format (kuruş gösterilmez)

## Veri Kaynakları ve Hesaplama Mantığı

### Temel Veri Akışı
```
VIEW_CP_EXCEL_FATURA (fatura listesi)
  + Varuna Closed siparişler (Excel'de henüz olmayanlar sentetik eklenir)
  ↓ Fatura_No = TBL_VARUNA_SIPARIS.SerialNumber (Closed + TotalNetAmount > 0)
  ↓
TBL_VARUNA_SIPARIS → TotalNetAmount (KDV hariç TL, sipariş başlığı)
  ↓ OrderId = TBL_VARUNA_SIPARIS_URUNLERI.CrmOrderId
  ↓
TBL_VARUNA_SIPARIS_URUNLERI → kalemler (döviz bazlı Total, StockCode)
  ↓ StockCode = TBLSOS_URUN_ESLESTIRME.StokKodu
  ↓
TBLSOS_URUN_ESLESTIRME → AnaUrunId → TBLSOS_ANA_URUN.Ad (ürün grubu)
```

### Fatura Kartı (Dip Toplam)
- **Kaynak:** `TBL_VARUNA_SIPARIS.TotalNetAmount` (KDV hariç TL); Varuna'da eşleşmeyen faturalar için fallback: `VIEW_CP_EXCEL_FATURA.Fatura_Toplam` (Excel)
- **Tüm faturalar dahil**: Varuna eşleşen + eşleşmeyen — sonuçta hepsi gerçek fatura
- Varuna'da eşleşmeyen faturalar **dip toplama dahil edilir**, ayrıca küçük bir not olarak ("Varuna dışı: N fatura · ₺X") gösterilir
- **İade/İptal/Ret durumlu faturalar HİÇ sayılmaz** (tutar 0, adet 0)
- **Referans:** Excel kaynak dosya tahmini referanstır — birebir uyumsuzluk normal olabilir (bazı faturalar Excel'de eski tarihli girilmiş, dashboard `Fatura_Tarihi` bazlı çalışır)

### Ürün Bazlı Fatura Dağılımı
- **Kaynak:** `TBL_VARUNA_SIPARIS_URUNLERI` kalemlerinden oransal TL dağıtımı
- **Hesap:** `(kalem.Total / toplamDöviz) * TotalNetAmount` = kalemin TL tutarı
- Her kalemin `StockCode` → `TBLSOS_URUN_ESLESTIRME` → ürün grubuna atanır
- **Fatura seviyesi:** Varuna'da eşleşmeyen faturalar (`VarunaEslesti=false`) ürün kırılımına GİRMEZ
- **Kalem seviyesi:** Eşleşen fatura içinde `StockCode` TBLSOS_URUN_ESLESTIRME'de bulunmazsa → **"Diğer"** kategorisine düşer (finansal tutarlılık için — kalemin TL değeri kaybolmamalı)
- **ÖNEMLİ:** Ürün kırılımı toplamı = Fatura kartı dip toplamı (tutarlı olmalı)

### Tahsilat Kartı
- **Kaynak:** `VIEW_CP_EXCEL_FATURA` — `Fatura_Toplam`, `Tahsil_Edilen`, `Bekleyen_Bakiye`, `Tahsil_Tarihi`, `Fatura_Vade_Tarihi`
- Bakiye: `Fatura_Toplam - Tahsil_Edilen`

### CEI Tahsilat Başarı Oranları (Haftalık/Aylık/YTD)
- **PAY:** `Tahsil_Tarihi` dönemde olan faturaların `SUM(Tahsil_Edilen)` toplamı
- **PAYDA:** `Fatura_Vade_Tarihi` ≤ dönem sonu & `Bekleyen_Bakiye > 0` → `SUM(Bekleyen_Bakiye)` + PAY
- PAYDA = dönem sonuna kadar vadesi gelen tüm alacak (tahsil edilenler + bekleyenler)
- Oran = PAY / PAYDA * 100
- Tüm kartlar aynı mantık, sadece tarih aralığı farklı

### Duplike Yönetimi
- `VIEW_CP_EXCEL_FATURA` → Fatura_No bazlı deduplicate (GroupBy First)
- `TBL_VARUNA_SIPARIS_URUNLERI` → CrmOrderId + StockCode bazlı GroupBy (kodda)
- `TBLSOS_URUN_ESLESTIRME` → StokKodu başına TEK kayıt (206 unique, Excel kaynak bazlı)

### Excel Referans Dosya
- **Yol:** `Excel /Satış Analizi Kaynak Liste (Ham veri) (2023-2026).Rev (1).xlsx`
- **Tahmini referans** — birebir doğrulama beklemeyin: bazı faturalar Excel'de eski tarihli girilmiş olabilir, dashboard ise `Fatura_Tarihi` bazlı çalışır. Aylık karşılaştırmada ±%2-5 sapma olağan.
- **Kolonlar:** A=SiparişID, D=Çeyrek, E=Yıl, I=ÜrünKodu, M=AnaÜrün, W=NetSatış
- Q1 2026 toplam: ₺102.3M (389 sipariş, 698 kalem)
- Bu dosya doğruluk referansıdır — dashboard rakamları bununla tutmalı

### Fırsat Analiz — Cockpit Tutarlılığı
- **Fatura kartı:** `CockpitController.LoadAllCachedDataAsync` ile aynı cached veriyi kullanır (İade/İptal/Ret filtresi + Varuna eşleşme + sentetik fallback dahil)
- **Teklif kartı:** Dönemdeki TÜM teklifler (`CreatedOn` bazlı) — fırsata bağlı olma zorunluluğu yok
- **Sipariş kartı:** Dönemdeki TÜM siparişler (`CreateOrderDate` dönemde VEYA Closed + efektif fatura tarihi dönemde) — zincir bağımlılığı yok
- **Fırsat kartı:** CRM `TBLSOS_VARUNA_FIRSAT_ODATA` bazlı, Lost ve kapalı siparişli olanlar hariç
- **Genel prensip:** Her aşama bağımsız — fırsatı/teklifi olmayan sipariş veya fatura olabilir

## Dosya Yapısı
- `Controllers/CockpitController.cs` — ana dashboard, single-pass metrics, AJAX endpoints, `LoadAllCachedDataAsync` (ortak veri kaynağı)
- `Controllers/FirsatAnalizController.cs` — fırsat hunisi, opportunity bazlı analiz
- `Views/Cockpit/Index.cshtml` — dashboard UI, JS AJAX callback
- `Views/FirsatAnaliz/Index.cshtml` — fırsat analiz UI, funnel kartları
- `Views/Shared/_Layout.cshtml` — global CSS/JS, sidebar, Apple-quality rendering
- `Models/ViewModels/CockpitViewModel.cs` — dashboard ViewModel
- `Services/TahakkukService.cs` — tahakkuk map cache + invalidate
- `Services/DatabaseMigrationService.cs` — tablo oluşturma + seed
- `DbData/MskDbContext.cs` — EF DbSets
