using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Web.Controllers
{
  public class CartController : Controller
  {
    public IActionResult Index()
    {
      return View();
    }
  }
}
