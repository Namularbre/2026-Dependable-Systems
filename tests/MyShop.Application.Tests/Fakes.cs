using MyShop.Application.Ports.Out;
using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;

namespace MyShop.Application.Tests;

// In-memory driven adapters: the hexagon is tested without any infrastructure.

internal sealed class InMemoryItems : IItemRepository
{
    public Dictionary<Guid, Item> Store { get; } = [];

    public Task<Item?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Store.GetValueOrDefault(id) is { IsDeleted: false } i ? i : null);

    public Task<IReadOnlyDictionary<Guid, Item>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Item>>(
            ids.Distinct().Where(Store.ContainsKey).Select(id => Store[id]).Where(i => !i.IsDeleted).ToDictionary(i => i.Id));

    public Task<IReadOnlyList<Item>> ListAsync(bool publishedOnly, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Item>>([.. Store.Values.Where(i => !i.IsDeleted && (!publishedOnly || i.IsPublished))]);

    public void Add(Item item) => Store.Add(item.Id, item);
    public void Update(Item item) => Store[item.Id] = item;
}

internal sealed class InMemoryMovements : IStockMovementRepository
{
    public List<StockMovement> Store { get; } = [];

    public void Add(StockMovement movement) => Store.Add(movement);

    public Task<IReadOnlyList<StockMovement>> ListByItemAsync(Guid itemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<StockMovement>>([.. Store.Where(m => m.ItemId == itemId)]);
}

internal sealed class InMemoryCarts : ICartRepository
{
    public Dictionary<Guid, Cart> Store { get; } = [];

    public Task<Cart?> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Store.GetValueOrDefault(userId));

    public Task SaveAsync(Cart cart, CancellationToken ct = default)
    {
        Store[cart.UserId] = cart;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryOrders : IOrderRepository
{
    public List<Order> Store { get; } = [];

    public void Add(Order order) => Store.Add(order);

    public Task<IReadOnlyList<Order>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>([.. Store.Where(o => o.UserId == userId)]);

    public Task<IReadOnlyList<Order>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Store);
}

/// <summary>Runs the work directly; records whether it committed.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Commits { get; private set; }

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct = default)
    {
        var result = await work(ct);
        if (result.IsSuccess) Commits++;
        return result;
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class ResultAssertions
{
    public static T Value<T>(this Result<T> result) =>
        result.Match(v => v, e => throw new ShouldAssertException($"Expected success but got {e.Code}: {e.Message}"));

    public static Error Error<T>(this Result<T> result) =>
        result.Match(v => throw new ShouldAssertException($"Expected failure but got {v}"), e => e);
}
