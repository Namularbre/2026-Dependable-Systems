using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyShop.Application.Ports.In;
using MyShop.Web.Infrastructure;

namespace MyShop.Web.Controllers;

[Authorize]
public sealed class CartController(ICartService carts, ICheckoutService checkout) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await carts.GetAsync(User.UserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Add(Guid itemId, int quantity = 1, CancellationToken ct = default) =>
        this.RedirectWithOutcome(
            await carts.AddAsync(User.UserId(), itemId, quantity, ct),
            cart => $"Added to cart ({cart.Count} item(s)).",
            RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> Update(Guid itemId, int quantity, CancellationToken ct) =>
        this.RedirectWithOutcome(
            await carts.ChangeQuantityAsync(User.UserId(), itemId, quantity, ct),
            _ => "Cart updated.",
            RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> Remove(Guid itemId, CancellationToken ct) =>
        this.RedirectWithOutcome(
            await carts.RemoveAsync(User.UserId(), itemId, ct),
            _ => "Item removed.",
            RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> Checkout(CancellationToken ct) =>
        (await checkout.PlaceOrderAsync(User.UserId(), ct)).Match<IActionResult>(
            order =>
            {
                this.Flash($"Thank you! Order {order.Id.ToString()[..8]} placed for {order.Total:C}.");
                return RedirectToAction(nameof(OrdersController.Index), "Orders");
            },
            error =>
            {
                this.Flash(error.Message, FlashKind.Danger);
                return RedirectToAction(nameof(Index));
            });
}
