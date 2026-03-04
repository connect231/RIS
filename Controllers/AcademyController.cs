using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniCP.Controllers
{
    [Authorize(Roles = "Academy,Admin")]
    public class AcademyController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Univera Akademi";
            return View();
        }
    }
}
