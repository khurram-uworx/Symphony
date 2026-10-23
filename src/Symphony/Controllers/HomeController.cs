using Microsoft.AspNetCore.Mvc;

namespace Symphony.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
