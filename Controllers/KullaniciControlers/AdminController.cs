using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniCP.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    public ActionResult Index()
    {
        return View();
    }
}