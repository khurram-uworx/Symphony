using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Symphony.Data;

namespace Symphony.Controllers;

[Authorize(Roles = AdminSeeder.AdminRole)]
public sealed class AdminController : Controller
{
    public IActionResult Index() => View();
}
