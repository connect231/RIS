# SOS — Sales Operating System

Finansal cockpit dashboard uygulaması. ASP.NET Core MVC (.NET 10), Razor Views, Tailwind CSS, SQL Server.

## Proje Amacı
Satış ekibinin fatura, tahsilat, sözleşme ve hedef takibini tek ekrandan yapabilmesi. Apple-kalitesinde 60fps UI.

## Teknoloji
- **Backend:** ASP.NET Core MVC, Entity Framework Core, IDbContextFactory (thread-safety), IMemoryCache (5 dk TTL), SemaphoreSlim
- **Frontend:** Razor Views, Tailwind CDN, vanilla JS (no React/Vue), requestAnimationFrame animasyonlar
- **DB:** SQL Server `10.135.140.17\yazdes` / `UNIVERA_CUSTOMER_PORTAL`
- **Target:** net10.0

## Kritik Mimari Kararlar

### DbContext Pattern
`AddDbContext` + `AddDbContextFactory(ServiceLifetime.Scoped)` birlikte kullanılıyor.
- `AddDbContext` → ClaimsFactory, LogService gibi scoped servisler için
- `IDbContextFactory` → CockpitController'da `using var db = _contextFactory.CreateDbContext()` ile bağımsız context

### Tahsilat Tarih Mantığı
- Fatura kartı → `Fatura_Tarihi` bazlı
- Tahsilat kartı → `Fatura_Vade_Tarihi` bazlı
- Vadesi geçmiş → `Fatura_Vade_Tarihi` < dönem başı VE bakiye > 0
- İade/Ret durumlu faturalar tahsilat hesaplarından HARİÇ
- **NOT:** Ödeme sözü tarihi mantığı projeden tamamen kaldırıldı — sadece `Fatura_Vade_Tarihi` kullanılır.

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
VIEW_CP_EXCEL_FATURA (fatura listesi, Fatura_Tarihi bazlı filtreleme)
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
- **Kaynak:** `TBL_VARUNA_SIPARIS.TotalNetAmount` (KDV hariç TL)
- **Koşul:** Sadece `VarunaEslesti = true` olan faturalar dahil
- Varuna'da eşleşmeyen faturalar dip toplama GİRMEZ, sadece not olarak gösterilir
- **Referans:** Excel kaynak dosyadaki "Net Satış" (W kolonu) ile birebir tutmalı

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
- **Kolonlar:** A=SiparişID, D=Çeyrek, E=Yıl, I=ÜrünKodu, M=AnaÜrün, W=NetSatış
- Q1 2026 toplam: ₺102.3M (389 sipariş, 698 kalem)
- Bu dosya doğruluk referansıdır — dashboard rakamları bununla tutmalı

## Dosya Yapısı
- `Controllers/CockpitController.cs` — ana dashboard, single-pass metrics, AJAX endpoints
- `Views/Cockpit/Index.cshtml` — dashboard UI, JS AJAX callback
- `Views/Shared/_Layout.cshtml` — global CSS/JS, sidebar, Apple-quality rendering
- `Models/ViewModels/CockpitViewModel.cs` — dashboard ViewModel
- `Services/DatabaseMigrationService.cs` — tablo oluşturma + seed
- `DbData/MskDbContext.cs` — EF DbSets
Context.cs` — EF DbSets
