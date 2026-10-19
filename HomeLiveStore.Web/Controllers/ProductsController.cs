using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Web.Controllers
{
  public class ProductsController : Controller
  {
    public IActionResult Index()
    {
      return View();
    }

    public IActionResult Detail(int id)
    {
      ViewData["ProductId"] = id;
      return View();
    }
  }
}
