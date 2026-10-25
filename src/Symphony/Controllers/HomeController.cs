using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Symphony.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Account");
}
