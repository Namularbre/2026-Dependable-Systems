using Microsoft.Extensions.Logging;
using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Carts;
using MyShop.Domain.Common;

namespace MyShop.Application.UseCases;

public sealed class CartService(
    ICartRepository carts,
    IItemRepository items,
    IUnitOfWork uow,
    ILogger<CartService> logger) : ICartService
{
    public async Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default) =>
        await carts.GetByUserAsync(userId, ct) is { } cart ? await ToDtoAsync(cart, ct) : CartDto.Empty;

    public Task<Result<CartDto>> AddAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct = default) =>
        MutateAsync(userId, nameof(AddAsync), (cart, token) =>
            Quantity.Create(quantity).BindAsync(q =>
                items.FindAsync(itemId, token).Bind(item => cart.Add(item, q))), ct);

    public Task<Result<CartDto>> ChangeQuantityAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct = default) =>
        MutateAsync(userId, nameof(ChangeQuantityAsync), (cart, _) =>
            Task.FromResult(Quantity.Create(quantity).Bind(q => cart.ChangeQuantity(itemId, q))), ct);

    public Task<Result<CartDto>> RemoveAsync(Guid userId, Guid itemId, CancellationToken ct = default) =>
        MutateAsync(userId, nameof(RemoveAsync), (cart, _) => Task.FromResult(cart.Remove(itemId)), ct);

    /// <summary>Load (or start) the user's cart, apply a pure change, persist it, project it.</summary>
    private Task<Result<CartDto>> MutateAsync(
        Guid userId,
        string useCase,
        Func<Cart, CancellationToken, Task<Result<Cart>>> change,
        CancellationToken ct) =>
        uow.ExecuteAsync(async token =>
                {
                    var cart = await carts.GetByUserAsync(userId, token) ?? Cart.Empty(userId);
                    return await change(cart, token).BindAsync(async updated =>
                    {
                        await carts.SaveAsync(updated, token);
                        return Result.Ok(await ToDtoAsync(updated, token));
                    });
                }, ct)
            .LogOutcome(logger, useCase, (log, dto) => log.CartChanged(userId, dto.Count));

    private async Task<CartDto> ToDtoAsync(Cart cart, CancellationToken ct) =>
        CartDto.From(cart, await items.GetManyAsync(cart.Lines.Select(l => l.ItemId), ct));
}
