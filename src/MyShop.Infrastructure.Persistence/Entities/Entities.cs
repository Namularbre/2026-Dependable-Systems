using Microsoft.AspNetCore.Identity;

namespace MyShop.Infrastructure.Persistence.Entities;

// Persistence models: mutable, EF-friendly shapes of the immutable domain records.

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    bool IsDeleted { get; set; }
}

public interface IVersioned
{
    long Version { get; set; }
}

public abstract class AuditableEntity : IAuditable
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Citus reference table: replicated on every node.</summary>
public sealed class ItemEntity : AuditableEntity, IVersioned
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsPublished { get; set; }
    public long Version { get; set; }
}

/// <summary>Citus distributed table, sharded by <see cref="ItemId"/>.</summary>
public sealed class StockMovementEntity : AuditableEntity
{
    public Guid ItemId { get; set; }
    public int Delta { get; set; }
    public required string Reason { get; set; }
}

/// <summary>Citus distributed table, sharded by <see cref="UserId"/>.</summary>
public sealed class CartEntity : AuditableEntity
{
    public Guid UserId { get; set; }
    public List<CartLineEntity> Lines { get; set; } = [];
}

/// <summary>Colocated with <see cref="CartEntity"/>: <see cref="UserId"/> is denormalized as the shard key.</summary>
public sealed class CartLineEntity : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid CartId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Citus distributed table, sharded by <see cref="UserId"/>, colocated with carts.</summary>
public sealed class OrderEntity : AuditableEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineEntity> Lines { get; set; } = [];
}

public sealed class OrderLineEntity : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ItemId { get; set; }
    public required string ItemName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public sealed class ShopUser : IdentityUser<Guid>, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ShopRole : IdentityRole<Guid>, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
