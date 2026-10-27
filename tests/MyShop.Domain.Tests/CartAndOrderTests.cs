using MyShop.Domain.Carts;
using MyShop.Domain.Common;
using MyShop.Domain.Items;
using MyShop.Domain.Orders;

namespace MyShop.Domain.Tests;

public sealed class CartAndOrderTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Adding_the_same_item_twice_merges_lines()
    {
        var mug = TestData.OnSale();

        var cart = Cart.Empty(UserId)
            .Add(mug, new Quantity(1))
            .Bind(c => c.Add(mug, new Quantity(2)))
            .Value();

        cart.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(new Quantity(3));
    }

    [Fact]
    public void Unpublished_item_cannot_be_added()
    {
        var hidden = Item.Create("Lamp", "", 30).Value();

        Cart.Empty(UserId).Add(hidden, new Quantity(1)).Error().Code.ShouldBe("item.not_on_sale");
    }

    [Fact]
    public void Quantity_per_line_is_capped()
    {
        var mug = TestData.OnSale(stock: 500);

        Cart.Empty(UserId).Add(mug, new Quantity(Cart.MaxQuantityPerLine + 1)).Error().Code.ShouldBe("cart.quantity_too_high");
    }

    [Fact]
    public void Remove_unknown_item_is_not_found() =>
        Cart.Empty(UserId).Remove(Guid.NewGuid()).Error().Kind.ShouldBe(ErrorKind.NotFound);

    [Fact]
    public void Place_snapshots_prices_withdraws_stock_and_records_sales()
    {
        var mug = TestData.OnSale("Mug", 10m, stock: 5);
        var pen = TestData.OnSale("Pen", 2.5m, stock: 10);
        var cart = Cart.Empty(UserId)
            .Add(mug, new Quantity(2))
            .Bind(c => c.Add(pen, new Quantity(4)))
            .Value();

        var placement = Order.Place(cart, new Dictionary<Guid, Item> { [mug.Id] = mug, [pen.Id] = pen }, Now).Value();

        placement.Order.UserId.ShouldBe(UserId);
        placement.Order.Total.ShouldBe(new Money(30m));
        placement.Order.PlacedAt.ShouldBe(Now);
        placement.UpdatedItems.Single(i => i.Id == mug.Id).Stock.ShouldBe(3);
        placement.UpdatedItems.Single(i => i.Id == pen.Id).Stock.ShouldBe(6);
        placement.Movements.Select(m => m.Delta).ShouldBe([-2, -4], ignoreOrder: true);
    }

    [Fact]
    public void Place_fails_atomically_when_one_line_lacks_stock()
    {
        var mug = TestData.OnSale(stock: 1);
        var cart = Cart.Empty(UserId).Add(mug, new Quantity(2)).Value();

        Order.Place(cart, new Dictionary<Guid, Item> { [mug.Id] = mug }, Now)
            .Error().Code.ShouldBe("item.insufficient_stock");
    }

    [Fact]
    public void Place_rejects_an_empty_cart() =>
        Order.Place(Cart.Empty(UserId), new Dictionary<Guid, Item>(), Now).Error().Code.ShouldBe("order.empty_cart");
}
