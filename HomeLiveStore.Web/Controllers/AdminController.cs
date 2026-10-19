using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Web.Controllers
{
  [Authorize(Roles = "Manager")]
  public class AdminController : Controller
  {
    public IActionResult Index()
    {
      return View();
    }

    public IActionResult Products()
    {
      return View();
    }

    public IActionResult Categories()
    {
      return View();
    }

    public IActionResult Orders()
    {
      return View();
    }

    public IActionResult Users()
    {
      return View();
    }
  }
}
