# Fırsat Analiz Sayfası — Teknik Dokümantasyon

Fırsattan Faturaya Uçtan Uca Dönüşüm İzleme

---

## 1. Genel Bakış

Fırsat Analiz sayfası, satış hunisini (funnel) 5 aşamada izler:

```
Fırsat → Teklif → Sipariş → Fatura
```

Her aşama **bağımsızdır** — fırsatı olmayan teklif, teklifi olmayan sipariş olabilir. Zincir zorunlu değil.

**Controller:** `Controllers/FirsatAnalizController.cs`
**View:** `Views/FirsatAnaliz/Index.cshtml`
**Veri Kaynağı:** Doğrudan EF Core sorguları (SP kullanılmaz, sadece Fatura kartı hariç)

---

## 2. Veri Tabloları

| Tablo | Açıklama | Anahtar Alanlar |
|-------|----------|-----------------|
| `TBL_VARUNA_OPPORTUNITIES` | CRM fırsatları | Id, OwnerId, OpportunityStageName, CloseDate, AmountAmount, Probability |
| `TBL_VARUNA_TEKLIFs` | Teklifler | Id, OpportunityId (fırsata bağlantı), Status, TotalNetAmountLocalCurrency_Amount, CreatedOn, ProposalOwnerId |
| `TBL_VARUNA_SIPARIs` | Siparişler | SerialNumber, QuoteId (teklife bağlantı), OrderStatus (Open/Closed/Canceled), TotalNetAmount, InvoiceDate, CreateOrderDate |
| `TBLSOS_FATURA_TAHAKKUK` | Tahakkuk override | SapReferansNo, FaturaNo, TahakkukTarihi |
| `TBLSOS_URUN_ESLESTIRME` | StockCode → Ana ürün eşleşme | StokKodu, AnaUrunId |
| `TBLSOS_ANA_URUN` | 8 ana ürün kategorisi | Id, Ad, Aktif |
| `TBLSOS_CRM_PERSON_ODATA` | CRM kişi bilgileri | Id → PersonNameSurname (owner çözümlemesi) |
| `TBLSOS_HEDEF_AYLIK` | Aylık hedefler | Yil, Ay, Tip, HedefTutar |

---

## 3. Veri Akışı ve Bağlantılar

```
TBL_VARUNA_OPPORTUNITIES (Fırsat)
  │ Id
  ▼
TBL_VARUNA_TEKLIFs (Teklif)
  │ OpportunityId → Fırsat.Id
  │ Id (QuoteId)
  ▼
TBL_VARUNA_SIPARIs (Sipariş)
  │ QuoteId → Teklif.Id
  │ SerialNumber (= Fatura_No)
  ▼
SP_COCKPIT_FATURA (Fatura — SP üzerinden)
  │ ICockpitDataService.GetFaturaOzetAsync()
```

**Owner çözümlemesi:**
```
OwnerId → TBLSOS_CRM_PERSON_ODATA.Id → PersonNameSurname
```

---

## 4. Filtre Mekanizması

### ParseFilter Fonksiyonu
Tüm endpoint'lerde ortak. Dönem başlangıç/bitiş tarihi hesaplar.

| Filtre Değeri | Açıklama | Tarih Aralığı |
|---------------|----------|---------------|
| `month` | Bu ay (varsayılan) | Ayın 1'i → Ayın son günü 23:59:59 |
| `lastmonth` | Geçen ay | Geçen ayın 1'i → son günü |
| `q1` ... `q4` | Çeyrekler | Çeyrek başı → sonu |
| `ytd` | Yıl başından bugüne | 1 Ocak → Bugün |
| `all` | Tüm dönem | 2020-01-01 → Bugün |
| `startDate + endDate` | Özel aralık | Girilen tarihler |

### Test/Deneme Filtreleri
Tüm sorgularda uygulanır:

```csharp
ExcludeTestFirsat: DeletedOn == null AND Name NOT LIKE '%TEST%/%DENEME%'
ExcludeTest:       DeletedOn == null AND Account_Title NOT LIKE '%TEST%/%DENEME%'
ExcludeTestSiparis: AccountTitle NOT LIKE '%TEST%/%DENEME%/%test%'
```

---

## 5. Huni Kartları (5 Kart)

### Kart 1: Tüm Fırsatlar (Açık Havuz)

**Kaynak:** `TBL_VARUNA_OPPORTUNITIES` — tüm zamanlar, dönem filtresi YOK

**Ana Rakam:** Açık havuzdaki fırsatların toplam tutarı
**Alt Metin:** Fırsat adedi

**Filtre Mantığı:**
- Won hariç (kazanılan fırsatlar düşülür)
- Lost hariç (kaybedilen fırsatlar düşülür)
- Closed stage hariç
- `kapaliSet` hariç (kapalı siparişi olan ve efektif tarihi dönem dışına kayan fırsatlar)
- Test/Deneme hariç

**Footer:** "X kazanılan (Y ₺) + Z kaybedilen (W ₺) hariç tutuldu"

**İlgili değişkenler:**
```
tumFirsatAdet, tumFirsatTutar      → Açık havuz
wonHavuzAdet, wonHavuzTutar         → Kazanılan (footer)
lostAdet, lostTutar                 → Kaybedilen (footer)
```

---

### Kart 2: Dönem Fırsat Potansiyeli

**Kaynak:** `TBL_VARUNA_OPPORTUNITIES` — `CloseDate` dönemde olanlar

**Ana Rakam:** Dönemdeki açık fırsatların toplam tutarı (Won+Lost hariç)
**Alt Metin:** Açık fırsat adedi

**Filtre Mantığı:**
- `CloseDate >= start AND CloseDate <= end`
- Lost hariç (dönemdeki)
- Closed stage hariç
- `kapaliSet` hariç (tahakkuk ile dönem dışına kayan)
- `eklenecekSet` dahil (tahakkuk ile dönem içine kayan Won fırsatlar)

**Footer:** "X kazanılan + Y kaybedilen hariç tutuldu"

**Arrow (Dönüşüm Oranı):** Kart 2 adet / Kart 1 adet × 100

**İlgili değişkenler:**
```
toplam, aktif, aktivTutar           → Açık fırsatlar
won, wonTutar                       → Kazanılan (footer)
donemLostAdet, donemLostTutar       → Kaybedilen (footer)
```

---

### Kart 3: Dönem Teklif

**Kaynak:** `TBL_VARUNA_TEKLIFs` — `CreatedOn` dönemde olanlar

**Ana Rakam:** Aktif tekliflerin toplam tutarı (reddedilenler hariç)
**Alt Metin:** Teklif adedi

**Filtre Mantığı:**
- `DeletedOn == null`
- `CreatedOn >= start AND CreatedOn <= end`
- Reject / Denied / Closed status hariç (reddedilenler)
- Fırsata bağlı olma zorunluluğu YOK — tüm dönem teklifleri

**Footer:** "X reddedilen (Y ₺ · %Z) hariç tutuldu"

**Arrow:** Kart 3 tutar / Kart 2 tutar × 100

**İlgili değişkenler:**
```
teklifToplam, teklifTutar           → Aktif teklifler
lostTeklifAdet, lostTeklifTutar     → Reddedilen (footer)
```

---

### Kart 4: Dönem Sipariş

**Kaynak:** `TBL_VARUNA_SIPARIs` — tahakkuk-aware filtreleme

**Ana Rakam:** Açık siparişlerin toplam tutarı (faturalananlar hariç)
**Alt Metin:** Açık sipariş adedi

**Filtre Mantığı:**
- Canceled hariç
- **Closed sipariş:** Efektif fatura tarihi dönemde ise dahil (tahakkuk override geçerli)
- **Open sipariş:** `CreateOrderDate` dönemde ise dahil
- Ana rakamda sadece Open siparişler gösterilir

**Footer:** "X faturalanan (Y ₺) hariç tutuldu"

**Arrow:** Kart 4 tutar / Kart 3 tutar × 100

**Tahakkuk Mantığı (EfektifInvoice):**
```
EfektifTarih = TahakkukMap[SerialNumber] ?? InvoiceDate
```
Eğer tahakkuk kaydı varsa, siparişin fatura tarihi override edilir.

**İlgili değişkenler:**
```
acikSiparisAdet, acikSiparisTutar   → Açık siparişler
faturalanmisAdet, faturalanmisTutar  → Faturalanan (footer)
```

---

### Kart 5: Dönem Satış Faturası

**Kaynak:** `ICockpitDataService.GetFaturaOzetAsync()` — **SP_COCKPIT_FATURA** (tek SP kullanan kart)

**Ana Rakam:** Faturalanan toplam tutar
**Alt Metin:** Faturalanan sipariş adedi

**Ayrı endpoint:** `GET /FirsatAnaliz/GetFaturaKarti` — ağır sorgu olduğu için paralel yüklenir

**Arrow:** Kart 5 tutar / Kart 4 tutar × 100

**İlgili değişkenler:**
```
kapaliSiparisAdet, kapaliSiparisTutar → Fatura verisi (SP'den)
gecenDonemFatura                      → Önceki dönem (trend karşılaştırma)
```

---

## 6. Tahakkuk & kapaliSet Mekanizması

Dashboard'un en karmaşık mantığı: bir fırsatın dönemine tahakkuk tarihine göre karar verilmesi.

### Akış:
```
1. Teklif → Sipariş zinciri: QuoteId eşleşmesi ile
2. Won fırsatlar: Teklif.Account_Title → Sipariş.AccountTitle eşleşmesi ile
3. Sipariş → Efektif tarih: TahakkukMap override veya InvoiceDate
4. kapaliSet: Efektif tarih dönem DIŞINDA → bu dönemden çıkar
5. eklenecekSet: Efektif tarih dönem İÇİNDE → bu döneme ekle
```

### Örnek:
- Fırsat CloseDate = Nisan 2026
- Sipariş InvoiceDate = Nisan 2026
- Tahakkuk kaydı: TahakkukTarihi = Mart 2026
- → Efektif tarih = Mart → Bu fırsat Nisan'dan düşer, Mart'a kayar

---

## 7. Hedef Karşılaştırma

Her kart için dönem hedefi DB'den gelir (`TBLSOS_HEDEF_AYLIK`). Kart tutar / hedef × 100 oranı hesaplanır ve renk kodu uygulanır:

| Oran | Renk | Anlam |
|------|------|-------|
| ≥ 80% | Yeşil | Hedefte |
| 50-80% | Sarı | Risk |
| < 50% | Kırmızı | Alarm |

---

## 8. Tüm Endpoint'ler

### Ana Kart Endpoint'leri

| Endpoint | URL | Tablo | Açıklama |
|----------|-----|-------|----------|
| GetOpportunitySummary | `/FirsatAnaliz/GetOpportunitySummary` | OPPORTUNITIES, TEKLIF, SIPARIS | 5 kartın tüm verisini döner |
| GetFaturaKarti | `/FirsatAnaliz/GetFaturaKarti` | SP_COCKPIT_FATURA (SP) | Kart 5 verisi (ayrı endpoint, ağır) |

### Analiz Endpoint'leri

| Endpoint | URL | Tablo | Açıklama |
|----------|-----|-------|----------|
| GetSalesCycleData | `/FirsatAnaliz/GetSalesCycleData` | OPPORTUNITIES, TEKLIF, SIPARIS | Fırsat→Teklif→Sipariş→Fatura dönüşüm süreleri (gün) |
| GetOwnerPerformance | `/FirsatAnaliz/GetOwnerPerformance` | OPPORTUNITIES, TEKLIF | Satış temsilcisi bazlı performans |
| GetFunnelBreakdown | `/FirsatAnaliz/GetFunnelBreakdown` | OPPORTUNITIES, TEKLIF, SIPARIS, URUN | Seçili huni aşamasının owner + ürün kırılımı |
| GetLeaderboard | `/FirsatAnaliz/GetLeaderboard` | TEKLIF | Top 10 satışçı sıralaması |

### Detay & Filtre Endpoint'leri

| Endpoint | URL | Tablo | Açıklama |
|----------|-----|-------|----------|
| GetOpportunityDetail | `/FirsatAnaliz/GetOpportunityDetail` | OPPORTUNITIES, TEKLIF | Fırsat listesi (sayfalı, filtrelenebilir) |
| GetOwnerFilterOptions | `/FirsatAnaliz/GetOwnerFilterOptions` | OPPORTUNITIES | Owner dropdown verileri |
| GetFilterOptions | `/FirsatAnaliz/GetFilterOptions` | TEKLIF, ANA_URUN | Kişi + ürün dropdown verileri |

### Grafik & Diğer

| Endpoint | URL | Tablo | Açıklama |
|----------|-----|-------|----------|
| GetChartData | `/FirsatAnaliz/GetChartData` | TEKLIF, TEKLIF_URUN | Trend/ürün/müşteri grafikleri |
| GetStatusBreakdown | `/FirsatAnaliz/GetStatusBreakdown` | TEKLIF / SIPARIS | Status dağılımı |
| GetRiskAlerts | `/FirsatAnaliz/GetRiskAlerts` | TEKLIF | 30+ gün güncellenmemiş, süresi dolmuş teklifler |
| GetKpiSummary | `/FirsatAnaliz/GetKpiSummary` | TEKLIF, SIPARIS | Teklif bazlı KPI özeti |
| GetPersonScorecard | `/FirsatAnaliz/GetPersonScorecard` | TEKLIF | Kişi bazlı detaylı karne |

### Debug

| Endpoint | URL | Açıklama |
|----------|-----|----------|
| TestKpi | `/FirsatAnaliz/TestKpi` | Status dağılımı debug |
| FieldAudit | `/FirsatAnaliz/FieldAudit` | Veri kalitesi raporu |

---

## 9. Cache Yapısı

- **TTL:** 5 dakika (`TimeSpan.FromMinutes(5)`)
- **Key Pattern:** `FirsatOppSummary_v2_{start}_{end}_{owner}`
- **Invalidation:** TTL sonunda otomatik. Manuel invalidation endpoint'i yok.
- **Ürün eşleştirme cache:** `SemaphoreSlim` ile double-check locking

---

## 10. UI Bileşenleri

### Filtre Çubuğu
```
[Geçen ay] [Bu ay ✓] [2. Çeyrek] [YTD] [Tümü]
```
Default: **Bu ay**. Tıklanınca tüm kartlar AJAX ile güncellenir.

### Huni Kartları
5 kart soldan sağa, aralarında dönüşüm oranı okları (`conv-circle`).
Her kart tıklanabilir → alt bölümde breakdown detayı açılır.

### Breakdown Paneli
Seçili kart için:
- **Owner kırılımı:** Satışçı bazlı adet/tutar dağılımı
- **Ürün kırılımı:** Ana ürün bazlı adet/tutar dağılımı
- **Müşteri kırılımı:** Firma bazlı adet/tutar dağılımı

### Satış Döngüsü Bölümü
4 aşamalı timeline: Fırsat → Teklif → Sipariş → Fatura
Her geçiş için ortalama gün sayısı gösterilir.

### Aylık Trend
12 aylık bar chart: Fırsat/Teklif/Sipariş adet ve tutar trendi.

---

## 11. Bilinen Davranışlar

1. **Excel ile fark normal:** Dashboard tahakkuk, kapaliSet ve test filtresi uygular. Excel saf CloseDate bazlı. ±%5-10 sapma beklenen.

2. **Won fırsatlar dönem kayabilir:** Tahakkuk tarihi fatura tarihinden farklıysa, fırsat farklı döneme atanır.

3. **Fırsatsız teklifler:** `OpportunityId == null` olan teklifler, Kart 3'te sayılır ama Kart 2'de sayılmaz.

4. **Kart 5 ayrı yüklenir:** SP sorgusu ağır olduğu için paralel endpoint'ten gelir (`GetFaturaKarti`).
