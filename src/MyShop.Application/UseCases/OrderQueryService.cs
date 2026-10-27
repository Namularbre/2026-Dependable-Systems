using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;

namespace MyShop.Application.UseCases;

public sealed class OrderQueryService(IOrderRepository orders) : IOrderQueryService
{
    public async Task<IReadOnlyList<OrderDto>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
        [.. (await orders.ListByUserAsync(userId, ct)).Select(OrderDto.From)];

    public async Task<IReadOnlyList<OrderDto>> ListAllAsync(CancellationToken ct = default) =>
        [.. (await orders.ListAllAsync(ct)).Select(OrderDto.From)];
}
