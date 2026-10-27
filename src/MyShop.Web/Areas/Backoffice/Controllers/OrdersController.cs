using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyShop.Application.Ports.In;
using MyShop.Web.Infrastructure;

namespace MyShop.Web.Areas.Backoffice.Controllers;

[Area("Backoffice")]
[Authorize(Roles = Roles.Admin)]
public sealed class OrdersController(IOrderQueryService orders) : Controller
{
    /// <summary>Cross-shard query: Citus fans it out to every worker and merges the results.</summary>
    public async Task<IActionResult> Index(CancellationToken ct) => View(await orders.ListAllAsync(ct));
}
