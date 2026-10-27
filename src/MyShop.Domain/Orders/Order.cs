using System.Collections.Immutable;
using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Domain.Orders;

public sealed record OrderLine(Guid Id, Guid ItemId, string ItemName, Money UnitPrice, Quantity Quantity) : Entity(Id)
{
    public Money Total => UnitPrice * Quantity;
}

/// <summary>A placed order. Sharded by <see cref="UserId"/>, colocated with the user's cart.</summary>
public sealed record Order(Guid Id, Guid UserId, ImmutableList<OrderLine> Lines, DateTimeOffset PlacedAt) : Entity(Id)
{
    public Money Total => Lines.Aggregate(Money.Zero, (sum, l) => sum + l.Total);

    public sealed record Placement(Order Order, IReadOnlyList<Item> UpdatedItems, IReadOnlyList<StockMovement> Movements);

    /// <summary>Pure checkout: withdraws stock for every line and snapshots prices into a new order.</summary>
    public static Result<Placement> Place(Cart cart, IReadOnlyDictionary<Guid, Item> items, DateTimeOffset now) =>
        cart.IsEmpty
            ? Error.Validation("order.empty_cart", "Your cart is empty.")
            : cart.Lines
                .Select(line => Result.FromNullable(items.GetValueOrDefault(line.ItemId),
                        Error.NotFound("item.not_found", "An item in your cart no longer exists."))
                    .Bind(item => item.Withdraw(line.Quantity))
                    .Map(updated => (Line: line, Item: updated)))
                .Sequence()
                .Map(withdrawn => new Placement(
                    new Order(
                        Guid.CreateVersion7(),
                        cart.UserId,
                        [.. withdrawn.Select(w => new OrderLine(Guid.CreateVersion7(), w.Item.Id, w.Item.Name, w.Item.Price, w.Line.Quantity))],
                        now),
                    [.. withdrawn.Select(w => w.Item)],
                    [.. withdrawn.Select(w => StockMovement.Sale(w.Item.Id, w.Line.Quantity))]));
}
