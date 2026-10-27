using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Domain.Tests;

public sealed class ItemTests
{
    [Fact]
    public void Create_starts_hidden_with_no_stock()
    {
        var item = Item.Create("  Mug ", "Ceramic", 12.499m).Value();

        item.Name.ShouldBe("Mug");
        item.Price.ShouldBe(new Money(12.50m));
        item.Stock.ShouldBe(0);
        item.IsPublished.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", 1, "item.name_required")]
    [InlineData("Mug", -1, "money.negative")]
    public void Create_rejects_invalid_input(string name, decimal price, string code) =>
        Item.Create(name, "", price).Error().Code.ShouldBe(code);

    [Fact]
    public void Refill_adds_stock_without_mutating_the_original()
    {
        var item = Item.Create("Mug", "", 1).Value();

        var refilled = item.Refill(new Quantity(7)).Value();

        refilled.Stock.ShouldBe(7);
        item.Stock.ShouldBe(0);
    }

    [Fact]
    public void Withdraw_more_than_stock_is_a_conflict()
    {
        var error = TestData.OnSale(stock: 2).Withdraw(new Quantity(3)).Error();

        error.Kind.ShouldBe(ErrorKind.Conflict);
        error.Code.ShouldBe("item.insufficient_stock");
    }

    [Fact]
    public void Withdraw_from_an_unpublished_item_fails()
    {
        var hidden = TestData.OnSale().Unpublish().Value();

        hidden.Withdraw(new Quantity(1)).Error().Code.ShouldBe("item.not_on_sale");
    }

    [Fact]
    public void Deleted_item_cannot_be_changed()
    {
        var deleted = TestData.OnSale().Delete().Value();

        deleted.IsDeleted.ShouldBeTrue();
        deleted.IsPublished.ShouldBeFalse();
        deleted.Refill(new Quantity(1)).Error().Kind.ShouldBe(ErrorKind.NotFound);
    }

    [Fact]
    public void Quantity_must_be_positive() =>
        Quantity.Create(0).Error().Code.ShouldBe("quantity.not_positive");
}
