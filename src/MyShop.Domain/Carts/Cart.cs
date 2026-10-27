using System.Collections.Immutable;
using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Domain.Carts;

public sealed record CartLine(Guid Id, Guid ItemId, Quantity Quantity) : Entity(Id);

/// <summary>A user's cart. Sharded by <see cref="UserId"/>, so every query carries it.</summary>
public sealed record Cart(Guid Id, Guid UserId, ImmutableList<CartLine> Lines) : Entity(Id)
{
    public const int MaxQuantityPerLine = 99;

    public static Cart Empty(Guid userId) => new(Guid.CreateVersion7(), userId, []);

    public bool IsEmpty => Lines.IsEmpty;

    public Result<Cart> Add(Item item, Quantity quantity) =>
        item.EnsureSellable().Bind(_ =>
            Lines.Find(l => l.ItemId == item.Id) switch
            {
                null => SetQuantity(new CartLine(Guid.CreateVersion7(), item.Id, quantity), quantity),
                var existing => SetQuantity(existing, existing.Quantity + quantity)
            });

    public Result<Cart> ChangeQuantity(Guid itemId, Quantity quantity) =>
        FindLine(itemId).Bind(line => SetQuantity(line, quantity));

    public Result<Cart> Remove(Guid itemId) =>
        FindLine(itemId).Map(line => this with { Lines = Lines.Remove(line) });

    public Cart Clear() => this with { Lines = [] };

    private Result<Cart> SetQuantity(CartLine line, Quantity quantity) =>
        quantity.Value > MaxQuantityPerLine
            ? Error.Validation("cart.quantity_too_high", $"At most {MaxQuantityPerLine} units per item.")
            : this with
            {
                Lines = Lines.Contains(line)
                    ? Lines.Replace(line, line with { Quantity = quantity })
                    : Lines.Add(line with { Quantity = quantity })
            };

    private Result<CartLine> FindLine(Guid itemId) =>
        Result.FromNullable(Lines.Find(l => l.ItemId == itemId),
            Error.NotFound("cart.line_not_found", "This item is not in your cart."));
}
