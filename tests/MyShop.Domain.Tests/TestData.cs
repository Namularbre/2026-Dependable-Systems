using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Domain.Tests;

internal static class TestData
{
    public static T Value<T>(this Result<T> result) =>
        result.Match(v => v, e => throw new ShouldAssertException($"Expected success but got {e.Code}: {e.Message}"));

    public static Error Error<T>(this Result<T> result) =>
        result.Match(v => throw new ShouldAssertException($"Expected failure but got {v}"), e => e);

    public static Item OnSale(string name = "Mug", decimal price = 10m, int stock = 5) =>
        Item.Create(name, "", price).Bind(i => i.Refill(new Quantity(stock))).Bind(i => i.Publish()).Value();
}
