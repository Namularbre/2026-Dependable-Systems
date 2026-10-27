using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyShop.Web.Models;

namespace MyShop.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction(nameof(CatalogController.Index), "Catalog");

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
