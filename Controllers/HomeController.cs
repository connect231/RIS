using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace UniCP.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View();
        }
    }
}
