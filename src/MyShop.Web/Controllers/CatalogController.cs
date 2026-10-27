using Microsoft.AspNetCore.Mvc;
using MyShop.Application.Ports.In;
using MyShop.Web.Infrastructure;

namespace MyShop.Web.Controllers;

public sealed class CatalogController(ICatalogService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await catalog.ListAvailableAsync(ct));

    public async Task<IActionResult> Details(Guid id, CancellationToken ct) =>
        this.ViewOrError(await catalog.GetAsync(id, ct), item => View(item));
}
