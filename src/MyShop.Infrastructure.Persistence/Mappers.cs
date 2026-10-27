using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;
using MyShop.Infrastructure.Persistence.Entities;

namespace MyShop.Infrastructure.Persistence;

internal static class Mappers
{
    private static T WithAudit<T>(this T record, IAuditable e) where T : Entity =>
        record with { CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, IsDeleted = e.IsDeleted };

    public static Item ToDomain(this ItemEntity e) =>
        new Item(e.Id, e.Name, e.Description, new Money(e.Price), e.Stock, e.IsPublished).WithAudit(e);

    public static ItemEntity ToEntity(this Item i) => new()
    {
        Id = i.Id, Name = i.Name, Description = i.Description, Price = i.Price.Amount, Stock = i.Stock,
        IsPublished = i.IsPublished, IsDeleted = i.IsDeleted
    };

    public static void Apply(this ItemEntity e, Item i)
    {
        e.Name = i.Name;
        e.Description = i.Description;
        e.Price = i.Price.Amount;
        e.Stock = i.Stock;
        e.IsPublished = i.IsPublished;
        e.IsDeleted = i.IsDeleted;
    }

    public static StockMovement ToDomain(this StockMovementEntity e) =>
        new StockMovement(e.Id, e.ItemId, e.Delta, Enum.Parse<StockMovementReason>(e.Reason)).WithAudit(e);

    public static StockMovementEntity ToEntity(this StockMovement m) =>
        new() { Id = m.Id, ItemId = m.ItemId, Delta = m.Delta, Reason = m.Reason.ToString() };

    public static Cart ToDomain(this CartEntity e) =>
        new Cart(e.Id, e.UserId,
            [.. e.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt)
                .Select(l => new CartLine(l.Id, l.ItemId, new Quantity(l.Quantity)).WithAudit(l))]).WithAudit(e);

    public static CartLineEntity ToEntity(this CartLine l, Cart cart) =>
        new() { Id = l.Id, UserId = cart.UserId, CartId = cart.Id, ItemId = l.ItemId, Quantity = l.Quantity.Value };

    public static Order ToDomain(this OrderEntity e) =>
        new Order(e.Id, e.UserId,
            [.. e.Lines.Select(l => new OrderLine(l.Id, l.ItemId, l.ItemName, new Money(l.UnitPrice), new Quantity(l.Quantity)).WithAudit(l))],
            e.PlacedAt).WithAudit(e);

    public static OrderEntity ToEntity(this Order o) => new()
    {
        Id = o.Id, UserId = o.UserId, PlacedAt = o.PlacedAt, Total = o.Total.Amount,
        Lines =
        [
            .. o.Lines.Select(l => new OrderLineEntity
            {
                Id = l.Id, UserId = o.UserId, OrderId = o.Id, ItemId = l.ItemId, ItemName = l.ItemName,
                UnitPrice = l.UnitPrice.Amount, Quantity = l.Quantity.Value
            })
        ]
    };
}
