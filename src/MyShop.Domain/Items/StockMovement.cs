using MyShop.Domain.Common;

namespace MyShop.Domain.Items;

public enum StockMovementReason { Refill, Sale }

/// <summary>Append-only audit trail of stock changes. Sharded by <see cref="ItemId"/>.</summary>
public sealed record StockMovement(Guid Id, Guid ItemId, int Delta, StockMovementReason Reason) : Entity(Id)
{
    public static StockMovement Refill(Guid itemId, Quantity quantity) =>
        new(Guid.CreateVersion7(), itemId, quantity.Value, StockMovementReason.Refill);

    public static StockMovement Sale(Guid itemId, Quantity quantity) =>
        new(Guid.CreateVersion7(), itemId, -quantity.Value, StockMovementReason.Sale);
}
