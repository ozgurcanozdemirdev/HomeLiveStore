using Microsoft.AspNetCore.Mvc;

namespace HomeLiveStore.Web.Controllers
{
  public class AccountController : Controller
  {
    public IActionResult Login(string? returnUrl = null)
    {
      ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
      return View();
    }

    public IActionResult Register(string? returnUrl = null)
    {
      ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
      return View();
    }
  }
}
