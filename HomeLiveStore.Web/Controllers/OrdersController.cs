using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Web.Controllers
{
  [Authorize]
  public class OrdersController : Controller
  {
    public IActionResult Index()
    {
      return View();
    }

    public IActionResult Detail(int id)
    {
      ViewData["OrderId"] = id;
      return View();
    }
  }
}
