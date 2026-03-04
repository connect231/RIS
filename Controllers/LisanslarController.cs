using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniCP.DbData;
using UniCP.Models.Lisans;
using System.Linq;
using System.Threading.Tasks;
using UniCP.Services;
using System.Security.Claims;
using System.Collections.Generic;
using System;

namespace UniCP.Controllers
{
    [Authorize(Roles = "Lisanslar,Admin")]
    public class LisanslarController : Controller
    {
        private readonly MskDbContext _context;
        private readonly IUrlEncryptionService _urlEncryption;
        private readonly ICompanyResolutionService _companyResolution;

        public LisanslarController(
            MskDbContext context, 
            IUrlEncryptionService urlEncryption,
            ICompanyResolutionService companyResolution)
        {
            _context = context;
            _urlEncryption = urlEncryption;
            _companyResolution = companyResolution;
        }

        public async Task<IActionResult> Index(string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);
            var kullanici = await _context.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            
            if (kullanici == null) return RedirectToAction("Login", "Account");

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD, 
                decryptedCompanyId, 
                HttpContext);

            var targetCompanies = companyResolution.TargetCompanyIds ?? new List<int>();

            // Pass filter to view for persistence in sidebar links
            ViewBag.SelectedCompanyId = decryptedCompanyId ?? companyResolution.SelectedCompanyId;

            // Handle cookie setting for filtered company
            if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (decryptedCompanyId.Value > 0 && targetCompanies.Contains(decryptedCompanyId.Value))
                {
                    _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            var baseQuery = _context.TBL_VARUNA_SOZLESMEs.AsQueryable();

            // Get all contract Ids (as strings, normalized to lowercase) that have files
            var contractIdsWithFiles = (await _context.TBL_VARUNA_SOZLESME_DOSYALARs
                .Select(f => f.ContractId)
                .Distinct()
                .ToListAsync())
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id.ToLowerInvariant())
                .ToHashSet();

            var rawResultList = await baseQuery.Select(x => new LisansContractVm
            {
                ContractId = x.LNGKOD,
                ContractNo = x.ContractNo,
                ContractName = x.ContractName,
                AccountTitle = x.AccountTitle,
                StartDate = x.StartDate,
                FinishDate = x.FinishDate,
                RenewalDate = x.RenewalDate,
                ContractStatus = x.ContractStatus, // Fetch raw first
                ContractUrl = x.ContractUrl,
                ContractType = (x.ContractType == "renewal" || x.ContractType == "Renewal") ? "Yenileme" : x.ContractType,
                TotalAmount = x.TotalAmount,
                ContractGuidId = x.Id.ToString()
            }).ToListAsync();

            // Set HasFiles based on separate query (case-insensitive)
            foreach (var item in rawResultList)
            {
                item.HasFiles = !string.IsNullOrEmpty(item.ContractGuidId) && 
                    contractIdsWithFiles.Contains(item.ContractGuidId.ToLowerInvariant());
            }

            bool isViewingAllAsAdmin = (kullanici.LNGKULLANICITIPI == 1 || kullanici.LNGKULLANICITIPI == 3) && 
                                       (!decryptedCompanyId.HasValue || decryptedCompanyId.Value <= 0);

            if (!isViewingAllAsAdmin)
            {
                if (!targetCompanies.Any())
                {
                    // Customer has no target companies
                    rawResultList = new List<LisansContractVm>();
                }
                else
                {
                    var authorizedNames = await _context.VIEW_ORTAK_PROJE_ISIMLERIs
                        .Where(x => targetCompanies.Contains(x.LNGKOD))
                        .Select(x => x.TXTORTAKPROJEADI)
                        .ToListAsync();
                    
                    var validNames = authorizedNames.Where(n => !string.IsNullOrEmpty(n)).ToList();

                    if (validNames.Any())
                    {
                        var searchTokens = new List<string>();
                        
                        foreach (var name in validNames)
                        {
                            // Tam isim eşleşmeleri
                            searchTokens.Add(name.ToUpper(new System.Globalization.CultureInfo("tr-TR")));
                            searchTokens.Add(name.ToUpper(new System.Globalization.CultureInfo("en-US")));

                            // "Sanipak Bos" gibi isimleri "SANİPAK SAĞLIKLI..." ile eşleştirebilmek için ilk kelimeyi al
                            var words = name.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            var firstWord = words.FirstOrDefault(w => w.Length >= 3);
                            if (firstWord != null)
                            {
                                searchTokens.Add(firstWord.ToUpper(new System.Globalization.CultureInfo("tr-TR")));
                                searchTokens.Add(firstWord.ToUpper(new System.Globalization.CultureInfo("en-US")));
                            }
                        }
                        searchTokens = searchTokens.Distinct().ToList();

                        rawResultList = rawResultList.Where(c => 
                        {
                            if (string.IsNullOrEmpty(c.AccountTitle)) return false;
                            
                            var titleTr = c.AccountTitle.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
                            var titleEn = c.AccountTitle.ToUpper(new System.Globalization.CultureInfo("en-US"));
                            
                            return searchTokens.Any(token => titleTr.Contains(token) || titleEn.Contains(token));
                        }).ToList();
                    }
                    else
                    {
                        rawResultList = new List<LisansContractVm>();
                    }
                }
            }

            // DEBUG: Log all raw statuses before mapping
            Console.WriteLine($"DEBUG: Raw result count: {rawResultList.Count}");
            foreach (var item in rawResultList)
            {
                Console.WriteLine($"DEBUG: ContractNo={item.ContractNo}, AccountTitle={item.AccountTitle}, RawStatus='{item.ContractStatus}'");
            }

            // Apply Status Mapping
            foreach (var item in rawResultList)
            {
                item.ContractStatus = GetTurkishContractStatus(item.ContractStatus);
            }

            // DEBUG: Log all mapped statuses
            foreach (var item in rawResultList)
            {
                Console.WriteLine($"DEBUG: ContractNo={item.ContractNo}, MappedStatus='{item.ContractStatus}'");
            }

            // Show archived and pending Univera signature contracts
            var filtered = rawResultList.Where(x => x.ContractStatus == "Arşivlendi" || x.ContractStatus == "Univera İmzasında").ToList();

            Console.WriteLine($"DEBUG: Filtered count: {filtered.Count}");

            return View(filtered);
        }

        private string GetTurkishContractStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return status;

            return status.Trim() switch
            {
                "InPreparation" => "Hazırlık Aşamasında",
                "SalesWaitingForInfo" => "Satışta - Bilgi Bekliyor",
                "PriceNegotiation" => "Fiyat Müzakere",
                "TextNegotiation" => "Metin Müzakere",
                "PendingUniveraSignature" => "Univera İmzasında",
                "Pending Univera Signature" => "Univera İmzasında", // Added variation with spaces
                "UniveraSignature" => "Univera İmzasında",
                "WaitingForUniveraSignature" => "Univera İmzasında",
                "PendingAccountSignature" => "Müşteri İmzasında",
                "NotExpired" => "Süresi Dolmadı",
                "NotTransferredForMaintenance" => "Bakıma Devir Olmadı",
                "Archieved" => "Arşivlendi", // Common typo in DB
                "Archived" => "Arşivlendi",
                "TerminationCancellation" => "Fesih / İptal",
                "RenewedExpired" => "Yenilendi / Süresi Doldu",
                "Coklu" => "Seçilmedi",
                _ => status
            };
        }
        [HttpGet]
        public async Task<IActionResult> ViewFile(string contractGuidId)
        {
            if (string.IsNullOrEmpty(contractGuidId)) 
                return NotFound("Sözleşme ID bulunamadı.");

            // Directly query the DOSYALAR table by ContractId string
            var file = await _context.TBL_VARUNA_SOZLESME_DOSYALARs
                .FirstOrDefaultAsync(f => f.ContractId == contractGuidId);
            
            if (file == null || string.IsNullOrEmpty(file.FileBase64)) 
                return NotFound("Sözleşmeye ait dosya bulunamadı.");

            try
            {
                byte[] fileBytes = Convert.FromBase64String(file.FileBase64);
                
                // Determine content type from extension
                string contentType = "application/octet-stream";
                if (!string.IsNullOrEmpty(file.FileExtension))
                {
                    string ext = file.FileExtension.ToLower().TrimStart('.');
                    contentType = ext switch
                    {
                        "pdf" => "application/pdf",
                        "doc" or "docx" => "application/msword",
                        "xls" or "xlsx" => "application/vnd.ms-excel",
                        "png" => "image/png",
                        "jpg" or "jpeg" => "image/jpeg",
                        _ => "application/octet-stream"
                    };
                }

                // Return inline (view in browser) instead of download
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error viewing contract file: {ex.Message}");
                return StatusCode(500, "Dosya görüntülenirken bir hata oluştu.");
            }
        }
    }
}

