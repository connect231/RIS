# Fırsat Analiz — Kod Referans Dokümanı

> Tarih: 2026-04-14  
> Kaynak dosyalar: `Controllers/FirsatAnalizController.cs`, `Views/FirsatAnaliz/Index.cshtml`

---

## BACKEND (FirsatAnalizController.cs)

### 1. EfektifInvoice — Tahakkuk Map Hesaplama (satır 104-109)

```csharp
private static DateTime? EfektifInvoice(string? serialNumber, DateTime? invoiceDate, Dictionary<string, DateTime> tahakkukMap)
{
    if (serialNumber != null && tahakkukMap.TryGetValue(serialNumber, out var th))
        return th;
    return invoiceDate;
}
```

### 2. ParseFilter Metodu (satır 113-195)

```csharp
private (DateTime start, DateTime end, string filter, int months) ParseFilter(string? filter, string? startDate, string? endDate)
{
    var now = DateTime.Now;
    var today = now.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
    var year = now.Year;
    DateTime start;
    DateTime end2;
    int months;
    var fmtP = System.Globalization.CultureInfo.InvariantCulture;
    var style = System.Globalization.DateTimeStyles.None;

    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate)
        && DateTime.TryParseExact(startDate, "yyyy-MM-dd", fmtP, style, out var sd)
        && DateTime.TryParseExact(endDate, "yyyy-MM-dd", fmtP, style, out var ed))
    {
        filter = "range";
        start = sd.Date;
        end2 = ed.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        months = Math.Max(1, (end2.Year - start.Year) * 12 + end2.Month - start.Month + 1);
        return (start, end2, filter, months);
    }

    switch (filter?.ToLowerInvariant())
    {
        case "ytd":
            start = new DateTime(year, 1, 1);
            end2 = today;
            months = now.Month;
            break;
        case "q1":
            start = new DateTime(year, 1, 1);
            end2 = new DateTime(year, 3, 31, 23, 59, 59);
            if (end2 > today) end2 = today;
            months = 3;
            break;
        case "q2":
            start = new DateTime(year, 4, 1);
            end2 = new DateTime(year, 6, 30, 23, 59, 59);
            if (end2 > today) end2 = today;
            months = 3;
            break;
        case "q3":
            start = new DateTime(year, 7, 1);
            end2 = new DateTime(year, 9, 30, 23, 59, 59);
            if (end2 > today) end2 = today;
            months = 3;
            break;
        case "q4":
            start = new DateTime(year, 10, 1);
            end2 = new DateTime(year, 12, 31, 23, 59, 59);
            if (end2 > today) end2 = today;
            months = 3;
            break;
        case "lastmonth":
            var lmMonth = now.Month == 1 ? 12 : now.Month - 1;
            var lmYear = now.Month == 1 ? year - 1 : year;
            start = new DateTime(lmYear, lmMonth, 1);
            end2 = new DateTime(lmYear, lmMonth, DateTime.DaysInMonth(lmYear, lmMonth), 23, 59, 59);
            months = 1;
            break;
        case "all":
            filter = "all";
            start = new DateTime(2020, 1, 1);
            end2 = today;
            months = (today.Year - 2020) * 12 + today.Month;
            break;
        case "week":
            var weekStart = today.AddDays(-(int)today.DayOfWeek + 1);
            if (weekStart > today) weekStart = weekStart.AddDays(-7);
            start = weekStart.Date;
            end2 = today;
            months = 1;
            break;
        default: // "month" veya null → Bu ay
            filter = "month";
            start = new DateTime(year, now.Month, 1);
            end2 = new DateTime(year, now.Month, DateTime.DaysInMonth(year, now.Month), 23, 59, 59);
            months = 1;
            break;
    }

    return (start, end2, filter ?? "month", months);
}
```

### 3. GetOpportunitySummary Metodu (satır 1418-1814)

```csharp
// GET /FirsatAnaliz/GetOpportunitySummary
[HttpGet]
public async Task<IActionResult> GetOpportunitySummary(string? filter, string? startDate, string? endDate, string? owner)
{
    var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

    // ── Cache kontrolü ──
    var cacheKey = $"FirsatOppSummary_v2_{start:yyyyMMdd}_{end:yyyyMMdd}_{owner ?? "all"}";
    if (_cache.TryGetValue(cacheKey, out object? cachedResult) && cachedResult != null)
        return Json(cachedResult);

    using var db = _contextFactory.CreateDbContext();
    var ownerMap = await GetOwnerMapAsync();

    // TBL_VARUNA_OPPORTUNITIES — firsat verileri
    var query = ExcludeTestFirsat(db.TBL_VARUNA_OPPORTUNITIESs.AsNoTracking())
        .Where(o => o.CloseDate.HasValue
            && o.CloseDate.Value >= start && o.CloseDate.Value <= end);

    if (!string.IsNullOrEmpty(owner))
        query = query.Where(o => o.OwnerId == owner);

    var data = await query.Select(o => new
    {
        o.Id, o.OwnerId, o.OpportunityStageName, DealType = o.DealType,
        DealTypeTR = (string?)null, o.Probability, o.CloseDate,
        CreatedOn = (DateTime?)null, o.AmountAmount, o.AccountId, o.Name
    }).ToListAsync();

    // Lost firsatlari ayir (kartlardan dus, analiz icin ayri tut)
    var donemLost = data.Where(d => d.OpportunityStageName == "Lost"
        || (d.OpportunityStageName != null && d.OpportunityStageName.Contains("Closed"))).ToList();

    // ── SATIS HUNISI: Firsat → Teklif → Siparis → Fatura zinciri ──

    // ── Tahakkuk-aware firsat filtresi ──
    var tahakkukMap = await _tahakkukService.GetTahakkukMapAsync();

    // Yol 1: Teklif(QuoteId) → Siparis zinciri
    var kapaliZincir = await ExcludeTest(db.TBL_VARUNA_TEKLIFs.AsNoTracking())
        .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue)
        .Join(ExcludeTestSiparis(db.TBL_VARUNA_SIPARIs.AsNoTracking())
            .Where(s => s.OrderStatus == "Closed" && s.QuoteId != null),
            t => t.Id.ToString(), s => s.QuoteId,
            (t, s) => new { OppId = t.OpportunityId!.Value.ToString().ToLower(), s.SerialNumber, s.InvoiceDate })
        .ToListAsync();

    // Yol 2: Won firsatlar → Teklif(Account_Title) → Siparis(AccountTitle) eslesmesi
    var wonOppIds = await ExcludeTestFirsat(db.TBL_VARUNA_OPPORTUNITIESs.AsNoTracking())
        .Where(o => o.OpportunityStageName == "Won")
        .Select(o => o.Id).ToListAsync();
    var wonOppIdSet = wonOppIds.Where(id => id != null).Select(id => id!.ToLower()).ToHashSet();

    var teklifMusteriMap = await ExcludeTest(db.TBL_VARUNA_TEKLIFs.AsNoTracking())
        .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue && t.Account_Title != null)
        .Select(t => new { OppId = t.OpportunityId!.Value.ToString().ToLower(), t.Account_Title })
        .ToListAsync();
    var oppMusteriMap = teklifMusteriMap
        .Where(t => wonOppIdSet.Contains(t.OppId))
        .GroupBy(t => t.OppId)
        .ToDictionary(g => g.Key, g => g.First().Account_Title!.Trim().ToLower());

    var closedSipEfektif = await ExcludeTestSiparis(db.TBL_VARUNA_SIPARIs.AsNoTracking())
        .Where(s => s.OrderStatus == "Closed" && s.SerialNumber != null && s.AccountTitle != null)
        .Select(s => new { s.SerialNumber, s.InvoiceDate, AccountTitle = s.AccountTitle!.Trim().ToLower() })
        .ToListAsync();
    var musteriEfektifMap = closedSipEfektif
        .GroupBy(s => s.AccountTitle)
        .ToDictionary(g => g.Key, g => {
            foreach (var s in g.OrderByDescending(x => x.InvoiceDate))
            {
                var ef = EfektifInvoice(s.SerialNumber, s.InvoiceDate, tahakkukMap);
                if (ef.HasValue && s.InvoiceDate.HasValue && ef.Value != s.InvoiceDate.Value)
                    return ef;
            }
            return g.OrderByDescending(x => x.InvoiceDate).First().InvoiceDate;
        });

    // OppId → Efektif tarih haritasi
    var kapaliOppEfektif = kapaliZincir
        .GroupBy(x => x.OppId)
        .ToDictionary(g => g.Key, g => {
            var first = g.First();
            return EfektifInvoice(first.SerialNumber, first.InvoiceDate, tahakkukMap);
        });
    // Won firsatlar: teklifteki musteri adi → siparisteki musteri → efektif tarih
    foreach (var kv in oppMusteriMap)
    {
        if (!kapaliOppEfektif.ContainsKey(kv.Key)
            && musteriEfektifMap.TryGetValue(kv.Value, out var efDate))
        {
            kapaliOppEfektif[kv.Key] = efDate;
        }
    }

    // ── kapaliSet ve eklenecekSet ──
    // Donem DISINA kayan kapali firsatlar → bu donemden cikar
    var kapaliSet = kapaliOppEfektif
        .Where(kv => kv.Value.HasValue && (kv.Value.Value < start || kv.Value.Value > end))
        .Select(kv => kv.Key).ToHashSet();
    // Donem ICINE kayan kapali firsatlar → bu doneme ekle (CloseDate donem disi ama efektif tarih donem ici)
    var eklenecekSet = kapaliOppEfektif
        .Where(kv => kv.Value.HasValue && kv.Value.Value >= start && kv.Value.Value <= end)
        .Select(kv => kv.Key).ToHashSet();

    // Donem firsatlari — tahakkukla donem disina kayanlar haric
    var dataAktif = data.Where(d => d.OpportunityStageName != "Lost"
        && (d.OpportunityStageName == null || !d.OpportunityStageName.Contains("Closed"))
        && !kapaliSet.Contains((d.Id ?? "").ToLower()))
        .ToList();

    // Tahakkukla bu doneme kayan Won firsatlari ekle
    var dataIdSet = dataAktif.Select(d => (d.Id ?? "").ToLower()).ToHashSet();
    var eklenecekFirsatlar = await ExcludeTestFirsat(db.TBL_VARUNA_OPPORTUNITIESs.AsNoTracking())
        .Where(o => o.OpportunityStageName == "Won" && eklenecekSet.Contains(o.Id!.ToLower()))
        .Select(o => new { o.Id, o.OwnerId, o.OpportunityStageName, DealType = o.DealType,
            DealTypeTR = (string?)null, o.Probability, o.CloseDate,
            CreatedOn = (DateTime?)null, o.AmountAmount, o.AccountId, o.Name })
        .ToListAsync();
    foreach (var ef in eklenecekFirsatlar)
    {
        if (!dataIdSet.Contains((ef.Id ?? "").ToLower()))
            dataAktif.Add(ef);
    }

    var toplam = dataAktif.Count;
    var wonList = dataAktif.Where(d => d.OpportunityStageName == "Won").ToList();
    var lostList = donemLost;
    var activeList = dataAktif.Where(d => d.OpportunityStageName != null
        && d.OpportunityStageName != "Won").ToList();
    var kazanmaOrani = (wonList.Count + lostList.Count) > 0
        ? Math.Round((decimal)wonList.Count / (wonList.Count + lostList.Count) * 100, 1) : 0m;
    var toplamFirsatTutar = dataAktif.Sum(d => d.AmountAmount ?? 0m);
    var wonTutar = wonList.Sum(d => d.AmountAmount ?? 0m);
    var lostTutar = lostList.Sum(d => d.AmountAmount ?? 0m);
    var aktivTutar = activeList.Sum(d => d.AmountAmount ?? 0m);
    var kazanmaOraniRevenue = (wonTutar + lostTutar) > 0
        ? Math.Round(wonTutar / (wonTutar + lostTutar) * 100, 1) : 0m;

    // ── PARALEL SORGULAR ──
    using var db2 = _contextFactory.CreateDbContext();
    using var db3 = _contextFactory.CreateDbContext();

    var tumFirsatTask = ExcludeTestFirsat(db2.TBL_VARUNA_OPPORTUNITIESs.AsNoTracking())
        .Select(o => new { o.Id, o.AmountAmount, o.OpportunityStageName, o.Probability })
        .ToListAsync();

    var firsatsizTeklifTask = ExcludeTest(db3.TBL_VARUNA_TEKLIFs.AsNoTracking())
        .Where(t => t.DeletedOn == null && t.OpportunityId == null)
        .GroupBy(t => 1)
        .Select(g => new { Adet = g.Count(), Tutar = g.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m) })
        .FirstOrDefaultAsync();

    var donemFirsatIdsTask = query
        .Where(o => o.OpportunityStageName != "Lost"
            && (o.OpportunityStageName == null || !o.OpportunityStageName.Contains("Closed")))
        .Select(o => o.Id).ToListAsync();

    await Task.WhenAll(tumFirsatTask, firsatsizTeklifTask, donemFirsatIdsTask);

    var tumFirsatlar = tumFirsatTask.Result;
    var firsatsizData = firsatsizTeklifTask.Result;
    var firsatsizTeklifAdet = firsatsizData?.Adet ?? 0;
    var firsatsizTeklifTutar = firsatsizData?.Tutar ?? 0m;

    // Acik havuz: Lost + Won + Closed haric
    var excludeStages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lost", "Won" };
    var acikFirsatlar = tumFirsatlar
        .Where(o => !kapaliSet.Contains((o.Id ?? "").ToLower())
            && !excludeStages.Contains(o.OpportunityStageName ?? "")
            && (o.OpportunityStageName == null || !o.OpportunityStageName.Contains("Closed")))
        .ToList();
    var tumFirsatAdet = acikFirsatlar.Count;
    var tumFirsatTutar = acikFirsatlar.Sum(o => o.AmountAmount ?? 0m);

    var lostFirsatlar = tumFirsatlar.Where(o => string.Equals(o.OpportunityStageName, "Lost", StringComparison.OrdinalIgnoreCase)).ToList();
    var lostAdet = lostFirsatlar.Count;
    var lostHavuzTutar = lostFirsatlar.Sum(o => o.AmountAmount ?? 0m);
    var wonFirsatlar = tumFirsatlar.Where(o => string.Equals(o.OpportunityStageName, "Won", StringComparison.OrdinalIgnoreCase)).ToList();
    var wonHavuzAdet = wonFirsatlar.Count;
    var wonHavuzTutar = wonFirsatlar.Sum(o => o.AmountAmount ?? 0m);

    // Donem firsat ID'leri — tahakkuk filtreleme
    var donemFirsatIds = donemFirsatIdsTask.Result
        .Where(id => !kapaliSet.Contains((id ?? "").ToLower())).ToList();
    var donemIdSetCheck = donemFirsatIds.Select(id => (id ?? "").ToLower()).ToHashSet();
    foreach (var ekId in eklenecekSet)
        if (!donemIdSetCheck.Contains(ekId)) donemFirsatIds.Add(ekId);
    var donemFirsatGuidSet = donemFirsatIds
        .Where(id => Guid.TryParse(id, out _))
        .Select(id => Guid.Parse(id))
        .ToHashSet();

    // Donem TUM teklifleri
    var donemTeklifler = await ExcludeTest(db.TBL_VARUNA_TEKLIFs.AsNoTracking())
        .Where(t => t.DeletedOn == null
            && t.CreatedOn.HasValue && t.CreatedOn.Value >= start && t.CreatedOn.Value <= end)
        .Select(t => new { t.Id, t.TotalNetAmountLocalCurrency_Amount, t.Status, t.OpportunityId, t.CreatedOn })
        .ToListAsync();
    var lostTeklifStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Reject", "Denied", "Closed" };
    var aktifTeklifler = donemTeklifler.Where(t => !lostTeklifStatuses.Contains(t.Status ?? "")).ToList();
    var lostTeklifler = donemTeklifler.Where(t => lostTeklifStatuses.Contains(t.Status ?? "")).ToList();
    var teklifToplam = aktifTeklifler.Count;
    var teklifTutar = aktifTeklifler.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
    var lostTeklifAdet = lostTeklifler.Count;
    var lostTeklifTutar = lostTeklifler.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

    // Agirlikli potansiyel
    var donemIdSet = donemFirsatIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var firsatProbMap = tumFirsatlar
        .Where(o => o.Id != null && donemIdSet.Contains(o.Id))
        .ToDictionary(o => o.Id!, o => o.Probability ?? 0m);

    var agirlikliPotansiyel = donemTeklifler.Sum(t => {
        var prob = t.OpportunityId.HasValue && firsatProbMap.TryGetValue(t.OpportunityId.Value.ToString(), out var p) ? p : 0m;
        return (t.TotalNetAmountLocalCurrency_Amount ?? 0m) * prob / 100m;
    });
    var ortOlasilik = donemTeklifler.Count > 0
        ? donemTeklifler.Average(t => {
            return t.OpportunityId.HasValue && firsatProbMap.TryGetValue(t.OpportunityId.Value.ToString(), out var p) ? p : 0m;
        }) : 0m;

    // Siparis karti: tahakkuk tutarli filtreleme
    var donemSiparislerRaw = await ExcludeTestSiparis(db.TBL_VARUNA_SIPARIs.AsNoTracking())
        .Where(s => s.OrderStatus != "Canceled")
        .Select(s => new { s.SerialNumber, s.TotalNetAmount, s.OrderStatus, s.InvoiceDate, s.CreateOrderDate })
        .ToListAsync();
    var donemSiparisler = donemSiparislerRaw.Select(s => new {
        s.SerialNumber, s.TotalNetAmount, s.OrderStatus,
        EfektifTarih = EfektifInvoice(s.SerialNumber, s.InvoiceDate, tahakkukMap),
        s.CreateOrderDate
    })
    .Where(s =>
        (s.OrderStatus == "Closed" && s.EfektifTarih.HasValue && s.EfektifTarih.Value >= start && s.EfektifTarih.Value <= end)
        || (s.OrderStatus == "Open" && s.CreateOrderDate.HasValue && s.CreateOrderDate.Value >= start && s.CreateOrderDate.Value <= end))
    .ToList();
    var acikSiparisAdet = donemSiparisler.Count(s => s.OrderStatus == "Open");
    var acikSiparisTutar = donemSiparisler.Where(s => s.OrderStatus == "Open").Sum(s => s.TotalNetAmount ?? 0m);
    var faturalanmisAdet = donemSiparisler.Count(s => s.OrderStatus == "Closed");
    var faturalanmisTutar = donemSiparisler.Where(s => s.OrderStatus == "Closed").Sum(s => s.TotalNetAmount ?? 0m);
    var toplamSiparisAdet = donemSiparisler.Count;
    var toplamSiparisTutar = donemSiparisler.Sum(s => s.TotalNetAmount ?? 0m);

    // Asama dagilimi + Anlasma tipi
    var stageDagilim = data.GroupBy(d => d.OpportunityStageName ?? "Bilinmiyor")
        .Select(g => new { asama = g.Key, adet = g.Count() }).OrderByDescending(x => x.adet).ToList();
    var dealTypeDagilim = data.GroupBy(d => d.DealTypeTR ?? d.DealType ?? "Bilinmiyor")
        .Select(g => new { tip = g.Key, adet = g.Count() }).OrderByDescending(x => x.adet).ToList();

    // ── PARALEL GRUP 2: Yillik trend sorgulari ──
    var yil = DateTime.Now.Year;
    using var db4 = _contextFactory.CreateDbContext();
    using var db5 = _contextFactory.CreateDbContext();

    var tumYilFirsatTask = ExcludeTestFirsat(db4.TBL_VARUNA_OPPORTUNITIESs.AsNoTracking())
        .Where(o => o.CloseDate.HasValue && o.CloseDate.Value.Year == yil && o.OpportunityStageName != "Lost")
        .Select(o => new { o.Id, o.CloseDate, o.OpportunityStageName, o.AmountAmount })
        .ToListAsync();
    var tumYilTeklifTask = ExcludeTest(db5.TBL_VARUNA_TEKLIFs.AsNoTracking())
        .Where(t => t.DeletedOn == null && t.CreatedOn.HasValue && t.CreatedOn.Value.Year == yil)
        .Select(t => new { t.CreatedOn, t.TotalNetAmountLocalCurrency_Amount })
        .ToListAsync();
    using var db6 = _contextFactory.CreateDbContext();
    var tumYilSiparisTask = ExcludeTestSiparis(db6.TBL_VARUNA_SIPARIs.AsNoTracking())
        .Where(s => s.CreateOrderDate.HasValue && s.CreateOrderDate.Value.Year == yil && s.OrderStatus != "Canceled")
        .Select(s => new { s.CreateOrderDate, s.OrderStatus, s.TotalNetAmount })
        .ToListAsync();

    await Task.WhenAll(tumYilFirsatTask, tumYilTeklifTask, tumYilSiparisTask);

    // ... aylik trend hesaplamalari ...

    // SP fatura ayri endpoint'e tasindi (GetFaturaKarti)
    var hedefTutar = await GetDonemHedefAsync(start, end);

    var result = new
    {
        // Kart 1: Tum firsatlar (acik havuz — Won + Lost haric)
        tumFirsatAdet, tumFirsatTutar, firsatsizTeklifAdet, firsatsizTeklifTutar,
        gecenDonemFatura = 0m,
        lostAdet, lostTutar = lostHavuzTutar, wonHavuzAdet, wonHavuzTutar,
        donemLostAdet = donemLost.Count, donemLostTutar = donemLost.Sum(d => d.AmountAmount ?? 0m),
        // Kart 2: Donem firsat potansiyeli
        toplam = activeList.Count + (aktivTutar < teklifTutar ? firsatsizTeklifAdet : 0),
        aktif = activeList.Count + (aktivTutar < teklifTutar ? firsatsizTeklifAdet : 0),
        won = wonList.Count, lost = lostList.Count,
        kazanmaOrani,
        toplamTutar = Math.Max(aktivTutar, teklifTutar),
        wonTutar, donemLostRevenue = lostTutar, aktivTutar, kazanmaOraniRevenue,
        // Kart 3: Donem teklif
        teklifToplam, teklifTutar, lostTeklifAdet, lostTeklifTutar,
        agirlikliPotansiyel, ortOlasilik = Math.Round(ortOlasilik, 1),
        // Kart 4: Donem siparis
        toplamSiparisAdet = acikSiparisAdet, toplamSiparisTutar = acikSiparisTutar,
        acikSiparisAdet, acikSiparisTutar, faturalanmisAdet, faturalanmisTutar,
        // Kart 5: Faturalanan — ayri endpoint (GetFaturaKarti)
        kapaliSiparisAdet = 0, kapaliSiparisTutar = 0m,
        hedefTutar,
        stageDagilim, dealTypeDagilim, aylikTrend
    };
    _cache.Set(cacheKey, result, CacheTTL);
    return Json(result);
}
```

### 4. GetFaturaKarti Metodu (satir 1817-1843)

```csharp
// GET /FirsatAnaliz/GetFaturaKarti — SP fatura (agir sorgu, ayri endpoint)
[HttpGet]
public async Task<IActionResult> GetFaturaKarti(string? filter, string? startDate, string? endDate)
{
    var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
    var cacheKey = $"FirsatFaturaKarti_{start:yyyyMMdd}_{end:yyyyMMdd}";
    if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null)
        return Json(cached);

    var prevDuration = end - start;
    var prevStart = start.AddDays(-prevDuration.TotalDays);
    var prevEnd = start.AddSeconds(-1);

    var spFaturaTask = _cockpitData.GetFaturaOzetAsync(start, end);
    var spPrevFaturaTask = _cockpitData.GetFaturaOzetAsync(prevStart, prevEnd);
    await Task.WhenAll(spFaturaTask, spPrevFaturaTask);

    var result = new
    {
        kapaliSiparisAdet = spFaturaTask.Result.Adet,
        kapaliSiparisTutar = spFaturaTask.Result.Toplam,
        gecenDonemFatura = spPrevFaturaTask.Result.Toplam
    };
    _cache.Set(cacheKey, result, CacheTTL);
    return Json(result);
}
```

### 5. kapaliSet ve eklenecekSet Olusumu (GetOpportunitySummary icinde, satir 1511-1534)

```csharp
// OppId → Efektif tarih haritasi
var kapaliOppEfektif = kapaliZincir
    .GroupBy(x => x.OppId)
    .ToDictionary(g => g.Key, g => {
        var first = g.First();
        return EfektifInvoice(first.SerialNumber, first.InvoiceDate, tahakkukMap);
    });

// Won firsatlar: teklifteki musteri adi → siparisteki musteri → efektif tarih
foreach (var kv in oppMusteriMap)
{
    if (!kapaliOppEfektif.ContainsKey(kv.Key)
        && musteriEfektifMap.TryGetValue(kv.Value, out var efDate))
    {
        kapaliOppEfektif[kv.Key] = efDate;
    }
}

// Donem DISINA kayan kapali firsatlar → bu donemden cikar
var kapaliSet = kapaliOppEfektif
    .Where(kv => kv.Value.HasValue && (kv.Value.Value < start || kv.Value.Value > end))
    .Select(kv => kv.Key).ToHashSet();

// Donem ICINE kayan kapali firsatlar → bu doneme ekle (CloseDate donem disi ama efektif tarih donem ici)
var eklenecekSet = kapaliOppEfektif
    .Where(kv => kv.Value.HasValue && kv.Value.Value >= start && kv.Value.Value <= end)
    .Select(kv => kv.Key).ToHashSet();
```

**Mantik ozeti:**
- `kapaliOppEfektif`: Kapali siparis olan firsatlar icin → `OppId → EfektifTarih` map'i. Iki yoldan doldurulur:
  1. `kapaliZincir` (Teklif.QuoteId → Siparis join)
  2. `oppMusteriMap` (Won firsat → Teklif.Account_Title → Siparis.AccountTitle eslesmesi)
- `kapaliSet`: Efektif tarihi donem **disinda** olan firsatlar → bu donem havuzundan **cikarilir**
- `eklenecekSet`: Efektif tarihi donem **icinde** olan firsatlar (CloseDate donem disi olsa bile) → bu doneme **eklenir**

---

## FRONTEND (Views/FirsatAnaliz/Index.cshtml)

### 1. Kart 5'i Yukleyen AJAX Cagrisi — loadFaturaKarti (satir 776-793)

```javascript
async function loadFaturaKarti() {
    try {
        var f = await fetch('/FirsatAnaliz/GetFaturaKarti?' + getFilterParams()).then(function(r) { return r.json(); });
        // Kart 5 guncelle
        countUp(document.getElementById('kpiVal5'), f.kapaliSiparisTutar, 700);
        document.getElementById('kpiSub5').textContent = f.kapaliSiparisAdet + ' faturalanan siparis';
        // Arrow 4→5
        var opp4val = parseFloat(document.getElementById('kpiVal4')?.dataset?.raw || '0');
        var cv4 = document.getElementById('kpiConv4');
        if (cv4 && opp4val > 0) {
            cv4.textContent = '%' + fmtPct.format(f.kapaliSiparisTutar / opp4val * 100);
            cv4.classList.remove('hidden');
        }
        // Hedef panelini fatura verisiyle guncelle
        window._faturaData = f;
        if (window._hedefTutar && window._hedefTutar > 0) updateHedefWithFatura(f);
    } catch(e) { console.error('FaturaKarti error:', e); }
}
```

### 2. Donusum Orani (conv-circle) Hesaplayan JavaScript Blogu (satir 933-983)

```javascript
// Arrow 1→2: donem %
var cv1 = document.getElementById('kpiConv1');
cv1.textContent = '%' + (opp.tumFirsatAdet > 0 ? fmtPct.format(opp.aktif / opp.tumFirsatAdet * 100) : '0');
cv1.classList.remove('hidden');

// Arrow 2→3: firsattan %
var cv2 = document.getElementById('kpiConv2');
cv2.textContent = '%' + (opp.toplamTutar > 0 ? fmtPct.format(opp.teklifTutar / opp.toplamTutar * 100) : '0');
cv2.classList.remove('hidden');

// Arrow 3→4: tekliften %
var cv3 = document.getElementById('kpiConv3');
cv3.textContent = '%' + (opp.teklifTutar > 0 ? fmtPct.format(opp.acikSiparisTutar / opp.teklifTutar * 100) : '0');
cv3.classList.remove('hidden');

// Arrow 4→5: (loadFaturaKarti icinde)
// cv4.textContent = '%' + fmtPct.format(f.kapaliSiparisTutar / opp4val * 100);

// Siparis tutar referansini sakla (arrow hesabi icin)
document.getElementById('kpiVal4').dataset.raw = opp.acikSiparisTutar;
```

**Donusum orani formulleri:**
| Ok | Formul |
|-----|--------|
| 1→2 | `aktif / tumFirsatAdet * 100` |
| 2→3 | `teklifTutar / toplamTutar * 100` |
| 3→4 | `acikSiparisTutar / teklifTutar * 100` |
| 4→5 | `kapaliSiparisTutar / acikSiparisTutar * 100` |

### 3. Hedef Analizi Yuzdesi Hesaplayan JavaScript Blogu (satir 994-1073 + 1110-1128)

```javascript
// ── HEDEF KARSILASTIRMA + ALARM + RENK KODLARI ──
var hedef = opp.hedefTutar || 0;
if (hedef > 0) {
    // Her kart icin hedef % hesapla ve renk kodla
    var kartlar = [
        { idx: 2, val: opp.toplamTutar, label: 'Firsat potansiyeli' },
        { idx: 3, val: opp.teklifTutar, label: 'Teklif' },
        { idx: 4, val: opp.toplamSiparisTutar, label: 'Siparis' },
        { idx: 5, val: window._faturaData ? window._faturaData.kapaliSiparisTutar : 0, label: 'Satis faturasi' }
    ];

    kartlar.forEach(function(k) {
        var pct = (k.val / hedef) * 100;
        var card = document.querySelector('[data-funnel="' + k.idx + '"]');
        if (!card) return;
        card.classList.remove('kpi-border-green','kpi-border-amber','kpi-border-red');
        if (pct >= 100) card.classList.add('kpi-border-green');
        else if (pct >= 85) card.classList.add('kpi-border-amber');
        else card.classList.add('kpi-border-red');
    });

    // ── HEDEF ANALIZ PANELI ──
    var faturaPct = hedef > 0 ? (fatVal / hedef) * 100 : 0;

    // Buyuk yuzde
    document.getElementById('hedefPctBig').textContent = '%' + fmtPct.format(faturaPct);
    document.getElementById('hedefPctBig').style.color =
        faturaPct >= 100 ? '#22d3a0' : faturaPct >= 85 ? '#f59e0b' : '#f87171';

    // Progress bar
    var barBig = document.getElementById('hedefBarBig');
    setTimeout(function() {
        barBig.style.width = Math.min(faturaPct, 100) + '%';
        barBig.style.background = faturaPct >= 100 ? '#22d3a0' : faturaPct >= 85 ? '#f59e0b' : '#f87171';
    }, 200);

    // Asama detaylari
    function pctColor(v) { return v >= 100 ? 'text-emerald-600' : v >= 85 ? 'text-amber-600' : 'text-red-500'; }
    var fp = opp.toplamTutar/hedef*100;
    var tp = opp.teklifTutar/hedef*100;
    var sp = opp.toplamSiparisTutar/hedef*100;
    var fap = faturaPct;
    safeHtml('hedefFirsatPct', abbr(opp.toplamTutar) + ' <span class="' + pctColor(fp) + '">%' + fmtPct.format(fp) + '</span>');
    safeHtml('hedefTeklifPct', abbr(opp.teklifTutar) + ' <span class="' + pctColor(tp) + '">%' + fmtPct.format(tp) + '</span>');
    safeHtml('hedefSiparisPct', abbr(opp.toplamSiparisTutar) + ' <span class="' + pctColor(sp) + '">%' + fmtPct.format(sp) + '</span>');
    safeHtml('hedefFaturaPct', abbr(fatVal) + ' <span class="' + pctColor(fap) + '">%' + fmtPct.format(fap) + '</span>');
}
```

**`updateHedefWithFatura(f)` — Fatura gelince hedef paneli gunceller (satir 1110-1128):**
```javascript
function updateHedefWithFatura(f) {
    var opp = window._oppData;
    var hedef = window._hedefTutar;
    if (!opp || !hedef || hedef <= 0) return;
    // Kart 5 renk
    var pct5 = (f.kapaliSiparisTutar / hedef) * 100;
    var card5 = document.querySelector('[data-funnel="5"]');
    if (card5) {
        card5.classList.remove('kpi-border-green','kpi-border-amber','kpi-border-red');
        card5.classList.add(pct5 >= 100 ? 'kpi-border-green' : pct5 >= 85 ? 'kpi-border-amber' : 'kpi-border-red');
    }
    // Hedef paneli
    var faturaPct = (f.kapaliSiparisTutar / hedef) * 100;
    var el = document.getElementById('hedefPctBig');
    if (el) { el.textContent = '%' + fmtPct.format(faturaPct); el.style.color = faturaPct >= 100 ? '#22d3a0' : faturaPct >= 85 ? '#f59e0b' : '#f87171'; }
    var bar = document.getElementById('hedefBarBig');
    if (bar) { bar.style.width = Math.min(faturaPct, 100) + '%'; bar.style.background = faturaPct >= 100 ? '#22d3a0' : faturaPct >= 85 ? '#f59e0b' : '#f87171'; }
    var ge = document.getElementById('hedefGerceklesen');
    if (ge) ge.textContent = abbr(f.kapaliSiparisTutar);
}
```

**Renk kodlari:**
| Yuzde | Renk | CSS Class |
|-------|------|-----------|
| >= 100% | Yesil (#22d3a0) | kpi-border-green |
| >= 85% | Amber (#f59e0b) | kpi-border-amber |
| < 85% | Kirmizi (#f87171) | kpi-border-red |

### 4. Filtre Butonlarina Tiklaninca Tum Kartlari Yenileyen Fonksiyon (satir 693-767 + 1700-1713)

```javascript
// Filtre parametrelerini olustur
function getFilterParams() {
    return 'filter=' + encodeURIComponent(activeFilter) + '&_t=' + Date.now();
}

// Tum kartlari paralel yukle
async function refreshAll() {
    var faturaPromise = loadFaturaKarti();
    window._faturaPromise = faturaPromise;
    await Promise.all([
        loadKpis(),                  // GetOpportunitySummary — Kart 1-4 + hedef
        faturaPromise,               // GetFaturaKarti — Kart 5
        loadFunnelBreakdown(activeFunnel > 0 ? activeFunnel : 2),  // Breakdown panel
        loadOppDetail(1),            // Detay tablosu
        loadOwnerOptions(),          // Owner dropdown
        loadSalesCycle()             // Satis dongusu
    ]);
}

// Filtre tab tiklandiginda
document.getElementById('filterTabs').addEventListener('click', function(e) {
    var btn = e.target.closest('.ftab');
    if (!btn) return;
    document.querySelectorAll('.ftab').forEach(function(b) { b.classList.remove('active'); });
    btn.classList.add('active');
    activeFilter = btn.dataset.filter;  // "month", "lastmonth", "q1", "q2", "ytd", "all" vb.
    // Funnel secimini sifirla
    activeFunnel = 0;
    document.querySelectorAll('.funnel-card').forEach(function(c) {
        c.classList.remove('active-card', 'dimmed');
    });
    refreshAll();  // ← Tum kartlar yeniden yuklenir
});
```

**Akis ozeti:**
1. Kullanici `.ftab` butonuna tiklar
2. `activeFilter` guncellenir (orn. "q2")
3. `refreshAll()` cagirilir
4. 6 paralel fetch baslar:
   - `loadKpis()` → `/FirsatAnaliz/GetOpportunitySummary?filter=q2` (Kart 1-4)
   - `loadFaturaKarti()` → `/FirsatAnaliz/GetFaturaKarti?filter=q2` (Kart 5)
   - `loadFunnelBreakdown()` → `/FirsatAnaliz/GetFunnelBreakdown?filter=q2`
   - `loadOppDetail()` → Firsat detay tablosu
   - `loadOwnerOptions()` → Owner dropdown
   - `loadSalesCycle()` → Satis dongusu analizi
5. KPI verisi gelince kartlar `countUp` ile animasyonlu guncellenir
6. Fatura verisi gelince Kart 5 + hedef paneli guncellenir
