using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;

namespace MyShop.Application.Ports.Out;

// Driven ports. Repositories only stage changes; IUnitOfWork commits them.

public interface IItemRepository
{
    Task<Item?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Item>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Item>> ListAsync(bool publishedOnly, CancellationToken ct = default);
    void Add(Item item);
    void Update(Item item);
}

public interface IStockMovementRepository
{
    void Add(StockMovement movement);
    Task<IReadOnlyList<StockMovement>> ListByItemAsync(Guid itemId, CancellationToken ct = default);
}

public interface ICartRepository
{
    Task<Cart?> GetByUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Inserts or reconciles the cart and its lines with the stored state.</summary>
    Task SaveAsync(Cart cart, CancellationToken ct = default);
}

public interface IOrderRepository
{
    void Add(Order order);
    Task<IReadOnlyList<Order>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListAllAsync(CancellationToken ct = default);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction and commits staged changes when it succeeds.
    /// A failed result rolls back; optimistic concurrency violations become <see cref="ErrorKind.Conflict"/>.
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct = default);
}
