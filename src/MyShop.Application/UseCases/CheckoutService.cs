using Microsoft.Extensions.Logging;
using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Orders;

namespace MyShop.Application.UseCases;

public sealed class CheckoutService(
    ICartRepository carts,
    IItemRepository items,
    IOrderRepository orders,
    IStockMovementRepository movements,
    IUnitOfWork uow,
    TimeProvider time,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    public async Task<Result<OrderDto>> PlaceOrderAsync(Guid userId, CancellationToken ct = default)
    {
        var result = await uow.ExecuteAsync(async token =>
        {
            var cart = await carts.GetByUserAsync(userId, token) ?? Cart.Empty(userId);
            var stock = await items.GetManyAsync(cart.Lines.Select(l => l.ItemId), token);

            return await Order.Place(cart, stock, time.GetUtcNow()).BindAsync(async placement =>
            {
                foreach (var item in placement.UpdatedItems) items.Update(item);
                foreach (var movement in placement.Movements) movements.Add(movement);
                orders.Add(placement.Order);
                await carts.SaveAsync(cart.Clear(), token);
                return Result.Ok(OrderDto.From(placement.Order));
            });
        }, ct);

        return result.Match(
            order => { logger.OrderPlaced(order.Id, userId, order.Total); return result; },
            error => { logger.CheckoutFailed(userId, error.Code, error.Message); return result; });
    }
}
