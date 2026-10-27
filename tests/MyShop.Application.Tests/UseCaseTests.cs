using Microsoft.Extensions.Logging.Abstractions;
using MyShop.Application.UseCases;

namespace MyShop.Application.Tests;

public sealed class UseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _userId = Guid.NewGuid();

    private readonly InMemoryItems _items = new();
    private readonly InMemoryMovements _movements = new();
    private readonly InMemoryCarts _carts = new();
    private readonly InMemoryOrders _orders = new();
    private readonly FakeUnitOfWork _uow = new();

    private BackofficeItemService Backoffice => new(_items, _movements, _uow, NullLogger<BackofficeItemService>.Instance);
    private CartService Carts => new(_carts, _items, _uow, NullLogger<CartService>.Instance);
    private CheckoutService Checkout => new(_carts, _items, _orders, _movements, _uow, new FixedTime(Now), NullLogger<CheckoutService>.Instance);
    private CatalogService Catalog => new(_items);

    [Fact]
    public async Task Create_with_initial_stock_records_a_refill_movement()
    {
        var item = (await Backoffice.CreateAsync(new CreateItemCommand("Mug", "", 10m, 5, Publish: true))).Value();

        item.Stock.ShouldBe(5);
        item.IsPublished.ShouldBeTrue();
        _movements.Store.ShouldHaveSingleItem().Delta.ShouldBe(5);
    }

    [Fact]
    public async Task Refill_increases_stock_and_logs_movement()
    {
        var item = (await Backoffice.CreateAsync(new CreateItemCommand("Mug", "", 10m, 0, Publish: false))).Value();

        var refilled = (await Backoffice.RefillAsync(new RefillItemCommand(item.Id, 3))).Value();

        refilled.Stock.ShouldBe(3);
        _movements.Store.ShouldHaveSingleItem().Delta.ShouldBe(3);
    }

    [Fact]
    public async Task Refill_with_invalid_quantity_does_not_commit()
    {
        var item = (await Backoffice.CreateAsync(new CreateItemCommand("Mug", "", 10m, 0, Publish: false))).Value();
        var commits = _uow.Commits;

        (await Backoffice.RefillAsync(new RefillItemCommand(item.Id, 0))).Error().Code.ShouldBe("quantity.not_positive");
        _uow.Commits.ShouldBe(commits);
    }

    [Fact]
    public async Task Hidden_and_deleted_items_are_not_in_the_catalog()
    {
        await Backoffice.CreateAsync(new CreateItemCommand("Visible", "", 1m, 1, Publish: true));
        await Backoffice.CreateAsync(new CreateItemCommand("Hidden", "", 1m, 1, Publish: false));
        var deleted = (await Backoffice.CreateAsync(new CreateItemCommand("Deleted", "", 1m, 1, Publish: true))).Value();
        await Backoffice.DeleteAsync(deleted.Id);

        (await Catalog.ListAvailableAsync()).Select(i => i.Name).ShouldBe(["Visible"]);
        (await Catalog.GetAsync(deleted.Id)).Error().Code.ShouldBe("item.not_found");
    }

    [Fact]
    public async Task Checkout_creates_the_order_decrements_stock_and_empties_the_cart()
    {
        var mug = (await Backoffice.CreateAsync(new CreateItemCommand("Mug", "", 10m, 5, Publish: true))).Value();
        (await Carts.AddAsync(_userId, mug.Id, 2)).Value().Total.ShouldBe(20m);

        var order = (await Checkout.PlaceOrderAsync(_userId)).Value();

        order.Total.ShouldBe(20m);
        order.PlacedAt.ShouldBe(Now);
        _items.Store[mug.Id].Stock.ShouldBe(3);
        (await Carts.GetAsync(_userId)).Lines.ShouldBeEmpty();
        _movements.Store.Select(m => m.Delta).ShouldBe([5, -2]);
    }

    [Fact]
    public async Task Checkout_fails_when_stock_ran_out_after_adding_to_cart()
    {
        var mug = (await Backoffice.CreateAsync(new CreateItemCommand("Mug", "", 10m, 2, Publish: true))).Value();
        await Carts.AddAsync(_userId, mug.Id, 2);
        _items.Update(_items.Store[mug.Id] with { Stock = 1 }); // someone else bought one

        (await Checkout.PlaceOrderAsync(_userId)).Error().Code.ShouldBe("item.insufficient_stock");
        _orders.Store.ShouldBeEmpty();
        (await Carts.GetAsync(_userId)).Lines.ShouldHaveSingleItem();
    }
}
