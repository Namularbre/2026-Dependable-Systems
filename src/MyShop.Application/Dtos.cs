using MyShop.Domain.Carts;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;

namespace MyShop.Application;

public sealed record ItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool IsAvailable => IsPublished && Stock > 0;

    public static ItemDto From(Item i) =>
        new(i.Id, i.Name, i.Description, i.Price.Amount, i.Stock, i.IsPublished, i.CreatedAt, i.UpdatedAt);
}

public sealed record StockMovementDto(Guid Id, int Delta, string Reason, DateTimeOffset CreatedAt)
{
    public static StockMovementDto From(StockMovement m) => new(m.Id, m.Delta, m.Reason.ToString(), m.CreatedAt);
}

public sealed record CartLineDto(Guid ItemId, string Name, decimal UnitPrice, int Quantity, int Stock, bool IsAvailable)
{
    public decimal Total => UnitPrice * Quantity;
}

public sealed record CartDto(IReadOnlyList<CartLineDto> Lines)
{
    public static readonly CartDto Empty = new([]);

    public decimal Total => Lines.Sum(l => l.Total);
    public int Count => Lines.Sum(l => l.Quantity);

    public static CartDto From(Cart cart, IReadOnlyDictionary<Guid, Item> items) =>
        new([
            .. from line in cart.Lines
               let item = items.GetValueOrDefault(line.ItemId)
               where item is not null
               select new CartLineDto(item.Id, item.Name, item.Price.Amount, line.Quantity.Value, item.Stock,
                   IsAvailable: item.IsPublished && item.Stock >= line.Quantity.Value)
        ]);
}

public sealed record OrderLineDto(Guid ItemId, string ItemName, decimal UnitPrice, int Quantity, decimal Total);

public sealed record OrderDto(Guid Id, Guid UserId, DateTimeOffset PlacedAt, decimal Total, IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto From(Order o) =>
        new(o.Id, o.UserId, o.PlacedAt, o.Total.Amount,
            [.. o.Lines.Select(l => new OrderLineDto(l.ItemId, l.ItemName, l.UnitPrice.Amount, l.Quantity.Value, l.Total.Amount))]);
}

public sealed record CreateItemCommand(string Name, string Description, decimal Price, int InitialStock, bool Publish);

public sealed record UpdateItemCommand(Guid Id, string Name, string Description, decimal Price);

public sealed record RefillItemCommand(Guid Id, int Quantity);
