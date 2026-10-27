using Microsoft.EntityFrameworkCore;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Carts;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;
using MyShop.Infrastructure.Persistence.Entities;

namespace MyShop.Infrastructure.Persistence;

// Reads used before a write are tracked, so the loaded Version becomes the concurrency check on save.

internal sealed class ItemRepository(ShopDbContext db) : IItemRepository
{
    public async Task<Item?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct))?.ToDomain();

    public async Task<IReadOnlyDictionary<Guid, Item>> GetManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var wanted = ids.Distinct().ToList();
        return wanted.Count == 0
            ? new Dictionary<Guid, Item>()
            : await db.Items.Where(i => wanted.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.ToDomain(), ct);
    }

    public async Task<IReadOnlyList<Item>> ListAsync(bool publishedOnly, CancellationToken ct = default) =>
        (await db.Items.AsNoTracking()
            .Where(i => !publishedOnly || i.IsPublished)
            .OrderBy(i => i.Name)
            .ToListAsync(ct))
        .ConvertAll(i => i.ToDomain());

    public void Add(Item item) => db.Items.Add(item.ToEntity());

    public void Update(Item item) =>
        (db.Items.Local.FirstOrDefault(e => e.Id == item.Id)
         ?? throw new InvalidOperationException($"Item {item.Id} must be loaded before being updated."))
        .Apply(item);
}

internal sealed class StockMovementRepository(ShopDbContext db) : IStockMovementRepository
{
    public void Add(StockMovement movement) => db.StockMovements.Add(movement.ToEntity());

    public async Task<IReadOnlyList<StockMovement>> ListByItemAsync(Guid itemId, CancellationToken ct = default) =>
        (await db.StockMovements.AsNoTracking()
            .Where(m => m.ItemId == itemId) // single-shard query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct))
        .ConvertAll(m => m.ToDomain());
}

internal sealed class CartRepository(ShopDbContext db) : ICartRepository
{
    public async Task<Cart?> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        (await db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.UserId == userId, ct))?.ToDomain();

    public async Task SaveAsync(Cart cart, CancellationToken ct = default)
    {
        var entity = db.Carts.Local.FirstOrDefault(c => c.Id == cart.Id)
                     ?? await db.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.UserId == cart.UserId && c.Id == cart.Id, ct);

        if (entity is null)
        {
            db.Carts.Add(new CartEntity { Id = cart.Id, UserId = cart.UserId, Lines = [.. cart.Lines.Select(l => l.ToEntity(cart))] });
            return;
        }

        var wanted = cart.Lines.ToDictionary(l => l.Id);
        foreach (var stored in entity.Lines.Where(l => !l.IsDeleted))
        {
            if (wanted.Remove(stored.Id, out var line)) stored.Quantity = line.Quantity.Value;
            else db.CartLines.Remove(stored); // logical delete via AuditInterceptor
        }
        foreach (var added in wanted.Values) db.CartLines.Add(added.ToEntity(cart));

        // Touch the cart itself so its UpdatedAt reflects line changes.
        db.Entry(entity).Property(c => c.UpdatedAt).IsModified = true;
    }
}

internal sealed class OrderRepository(ShopDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order.ToEntity());

    public async Task<IReadOnlyList<Order>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        (await db.Orders.AsNoTracking().Include(o => o.Lines)
            .Where(o => o.UserId == userId) // routed to a single shard
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync(ct))
        .ConvertAll(o => o.ToDomain());

    public async Task<IReadOnlyList<Order>> ListAllAsync(CancellationToken ct = default) =>
        (await db.Orders.AsNoTracking().Include(o => o.Lines)
            .OrderByDescending(o => o.PlacedAt)
            .Take(500)
            .ToListAsync(ct))
        .ConvertAll(o => o.ToDomain());
}
