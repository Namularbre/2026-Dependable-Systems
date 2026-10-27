using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyShop.Application.Ports.In;
using MyShop.Web.Infrastructure;

namespace MyShop.Web.Controllers;

[Authorize]
public sealed class OrdersController(IOrderQueryService orders) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await orders.ListForUserAsync(User.UserId(), ct));
}
