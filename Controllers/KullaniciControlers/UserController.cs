using System.Threading.Tasks;
using UniCP.Models;
using UniCP.Models.Kullanici;
using UniCP.Models.Kullanici.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using UniCP.DbData;
using UniCP.Models.MsK;

namespace dotnet_store.Controllers;

[Authorize(Roles = "User,Admin")]
public class UserController : Controller
{
    private UserManager<AppUser> _userManager;
    private RoleManager<AppRole> _roleManager;
    private readonly MskDbContext _mskDb;

    public UserController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, MskDbContext mskDb)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _mskDb = mskDb;
    }

    public async Task<ActionResult> Index(string role)
    {
        ViewBag.Roller = new SelectList(_roleManager.Roles, "Name", "Name", role);

        if (!string.IsNullOrEmpty(role))
        {
            return View(await _userManager.GetUsersInRoleAsync(role));
        }

        return View(_userManager.Users);
    }

    public ActionResult Create()
    {
        var projects = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.OrderBy(p => p.TXTORTAKPROJEADI).ToList();
        ViewBag.FirmaKod = new SelectList(projects, "LNGKOD", "TXTORTAKPROJEADI");
        return View();
    }

    [HttpPost]
    public async Task<ActionResult> Create(UserCreateModel model)
    {
        var projects = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.OrderBy(p => p.TXTORTAKPROJEADI).ToList();
        ViewBag.FirmaKod = new SelectList(projects, "LNGKOD", "TXTORTAKPROJEADI");

        if (ModelState.IsValid)
        {
            var user = new AppUser { UserName = model.Email, Email = model.Email, AdSoyad = model.AdSoyad, EmailConfirmed = true };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Sync to TBL_KULLANICI
                try 
                {
                    var mskUser = new TBL_KULLANICI
                    {
                        LNGIDENTITYKOD = user.Id,
                        TXTADSOYAD = model.AdSoyad,
                        TXTEMAIL = model.Email,
                        LNGORTAKFIRMAKOD = model.LNGORTAKFIRMAKOD, // Selected Project
                        LNGKULLANICITIPI = model.LNGKULLANICITIPI, // 1=Admin, 2=Kullanici
                        TXTFIRMAADI = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.FirstOrDefault(p => p.LNGKOD == model.LNGORTAKFIRMAKOD)?.TXTORTAKPROJEADI
                    };
                    
                    _mskDb.TBL_KULLANICIs.Add(mskUser);
                    _mskDb.SaveChanges(); // Get ID

                    // Handle Multi-Company for Univera User (Type 3) and Univera Customer (Type 4)
                    if ((model.LNGKULLANICITIPI == 3 || model.LNGKULLANICITIPI == 4) && model.SelectedCompanyIds != null && model.SelectedCompanyIds.Any())
                    {
                        foreach (var companyId in model.SelectedCompanyIds)
                        {
                            _mskDb.TBL_KULLANICI_FIRMAs.Add(new TBL_KULLANICI_FIRMA
                            {
                                LNGKULLANICIKOD = mskUser.LNGKOD,
                                LNGFIRMAKOD = companyId
                            });
                        }
                        _mskDb.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Kullanıcı oluşturuldu fakat detaylar kaydedilemedi: " + ex.Message);
                    return View(model);
                }

                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }
        model.Password = null;
        return View(model);
    }

    public async Task<ActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            return RedirectToAction("Index");
        }

        ViewBag.Roles = await _roleManager.Roles.Select(i => i.Name).ToListAsync();
        
        // Fetch TBL_KULLANICI details
        var mskUser = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == user.Id);
        // ViewBag.SelectedFirma logic removed, Model binding handles it
        
        // Pass plain list, View will select based on Model.LNGORTAKFIRMAKOD
        var projects = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.OrderBy(p => p.TXTORTAKPROJEADI).ToList();
        ViewBag.FirmaKod = new SelectList(projects, "LNGKOD", "TXTORTAKPROJEADI");

        return View(
            new UserEditModel
            {
                AdSoyad = user.AdSoyad,
                Email = user.Email!,
                SelectedRoles = await _userManager.GetRolesAsync(user),
                LNGORTAKFIRMAKOD = mskUser?.LNGORTAKFIRMAKOD,
                LNGKULLANICITIPI = mskUser?.LNGKULLANICITIPI,
                SelectedCompanyIds = mskUser != null 
                    ? _mskDb.TBL_KULLANICI_FIRMAs.Where(f => f.LNGKULLANICIKOD == mskUser.LNGKOD).Select(f => f.LNGFIRMAKOD).ToList()
                    : new List<int>()
            }
        );
    }

    [HttpPost]
    public async Task<ActionResult> Edit(string id, UserEditModel model)
    {
        var projects = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.OrderBy(p => p.TXTORTAKPROJEADI).ToList();
        ViewBag.FirmaKod = new SelectList(projects, "LNGKOD", "TXTORTAKPROJEADI");
        ViewBag.Roles = await _roleManager.Roles.Select(i => i.Name).ToListAsync();

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                user.Email = model.Email;
                user.AdSoyad = model.AdSoyad;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded && !string.IsNullOrEmpty(model.Password))
                {
                    // parola güncelle
                    await _userManager.RemovePasswordAsync(user);
                    await _userManager.AddPasswordAsync(user, model.Password);
                }

                if (result.Succeeded)
                {
                    await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));
                    if (model.SelectedRoles != null)
                    {
                        await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                    }
                    
                    // Sync TBL_KULLANICI
                    try 
                    {
                        var mskUser = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == user.Id);
                        if (mskUser == null)
                        {
                            mskUser = new TBL_KULLANICI { LNGIDENTITYKOD = user.Id };
                            _mskDb.TBL_KULLANICIs.Add(mskUser);
                        }
                        
                        mskUser.TXTADSOYAD = model.AdSoyad;
                        mskUser.TXTEMAIL = model.Email;
                        mskUser.LNGORTAKFIRMAKOD = model.LNGORTAKFIRMAKOD; // Selected Project
                        mskUser.LNGKULLANICITIPI = model.LNGKULLANICITIPI; // 1=Admin, 2=Kullanici
                        mskUser.TXTFIRMAADI = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.FirstOrDefault(p => p.LNGKOD == model.LNGORTAKFIRMAKOD)?.TXTORTAKPROJEADI;
                        
                        _mskDb.SaveChanges();

                        // Handle Multi-Company
                        // 1. Clear existing
                        var existingCompanies = _mskDb.TBL_KULLANICI_FIRMAs.Where(f => f.LNGKULLANICIKOD == mskUser.LNGKOD).ToList();
                        if (existingCompanies.Any()) _mskDb.TBL_KULLANICI_FIRMAs.RemoveRange(existingCompanies);
                        
                        // 2. Add New
                        // Handle Multi-Company for Univera User (Type 3) and Univera Customer (Type 4)
                        if ((model.LNGKULLANICITIPI == 3 || model.LNGKULLANICITIPI == 4) && model.SelectedCompanyIds != null && model.SelectedCompanyIds.Any())
                        {
                            foreach (var companyId in model.SelectedCompanyIds)
                            {
                                _mskDb.TBL_KULLANICI_FIRMAs.Add(new TBL_KULLANICI_FIRMA
                                {
                                    LNGKULLANICIKOD = mskUser.LNGKOD,
                                    LNGFIRMAKOD = companyId
                                });
                            }
                            _mskDb.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                         ModelState.AddModelError("", "Kullanıcı güncellendi fakat detaylar kaydedilemedi: " + ex.Message);
                         return View(model);
                    }

                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
        }

        model.Password = null;
        model.ConfirmPassword = null;
        return View(model);
    }


    public async Task<ActionResult> Delete(string id)
    {
        if (id == null)
        {
            return RedirectToAction("Index");
        }

        var entity = await _userManager.FindByIdAsync(id);

        if (entity != null)
        {
            return View(entity);
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<ActionResult> DeleteConfirm(string id)
    {
        if (id == null)
        {
            return RedirectToAction("Index");
        }

        var entity = await _userManager.FindByIdAsync(id);

        if (entity != null)
        {
            var result = await _userManager.DeleteAsync(entity);

            if (result.Succeeded)
            {
                try
                {
                    var mskUser = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == entity.Id);
                    if (mskUser != null)
                    {
                        _mskDb.TBL_KULLANICIs.Remove(mskUser);
                        _mskDb.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue since main user is deleted
                    // Consider adding a warning message to TempData if critical
                }

                TempData["Mesaj"] = $"{entity.AdSoyad} isimli kişi silindi.";
            }

        }
        return RedirectToAction("Index");
    }


}
