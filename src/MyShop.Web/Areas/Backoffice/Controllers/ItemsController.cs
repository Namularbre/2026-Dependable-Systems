using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyShop.Application;
using MyShop.Application.Ports.In;
using MyShop.Domain.Common;
using MyShop.Web.Areas.Backoffice.Models;
using MyShop.Web.Infrastructure;

namespace MyShop.Web.Areas.Backoffice.Controllers;

[Area("Backoffice")]
[Authorize(Roles = Roles.Admin)]
public sealed class ItemsController(IBackofficeItemService items) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await items.ListAsync(ct));

    public IActionResult Create() => View(new ItemForm { Publish = true });

    [HttpPost]
    public async Task<IActionResult> Create(ItemForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        switch (await items.CreateAsync(form.ToCreateCommand(), ct))
        {
            case Result<ItemDto>.Success(var item):
                this.Flash($"'{item.Name}' created.");
                return RedirectToAction(nameof(Index));
            case Result<ItemDto>.Failure(var error):
                this.AddError(error);
                return View(form);
            default:
                throw new InvalidOperationException();
        }
    }

    public Task<IActionResult> Edit(Guid id, CancellationToken ct) => EditView(id, form: null, ct);

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, [Bind(Prefix = nameof(ItemEditViewModel.Form))] ItemForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            switch (await items.UpdateAsync(form.ToUpdateCommand(id), ct))
            {
                case Result<ItemDto>.Success(var item):
                    this.Flash($"'{item.Name}' saved.");
                    return RedirectToAction(nameof(Index));
                case Result<ItemDto>.Failure(var error):
                    this.AddError(error);
                    break;
            }
        }
        return await EditView(id, form, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Refill(Guid id, int quantity, CancellationToken ct) =>
        this.RedirectWithOutcome(
            await items.RefillAsync(new RefillItemCommand(id, quantity), ct),
            item => $"'{item.Name}' refilled, {item.Stock} in stock.",
            RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, bool published, CancellationToken ct) =>
        this.RedirectWithOutcome(
            await items.SetPublishedAsync(id, published, ct),
            item => item.IsPublished ? $"'{item.Name}' is now on sale." : $"'{item.Name}' withdrawn from sale.",
            RedirectToAction(nameof(Index)));

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        this.RedirectWithOutcome(
            await items.DeleteAsync(id, ct),
            _ => "Item deleted.",
            RedirectToAction(nameof(Index)));

    private async Task<IActionResult> EditView(Guid id, ItemForm? form, CancellationToken ct) =>
        await items.GetAsync(id, ct) switch
        {
            Result<ItemDto>.Success(var item) => View(nameof(Edit),
                new ItemEditViewModel(form ?? ItemForm.From(item), item, await items.ListMovementsAsync(id, ct))),
            var failure => this.ViewOrError(failure, _ => NotFound())
        };
}
