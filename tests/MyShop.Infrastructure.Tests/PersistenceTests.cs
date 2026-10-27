using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyShop.Application;
using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Common;
using MyShop.Infrastructure.Persistence;

namespace MyShop.Infrastructure.Tests;

[Collection(CitusCollection.Name)]
public sealed class PersistenceTests(CitusFixture fixture)
{
    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = fixture.Scope();
        return await action(scope.ServiceProvider);
    }

    private Task<Result<ItemDto>> CreateItemAsync(int stock = 5) =>
        InScope(sp => sp.GetRequiredService<IBackofficeItemService>()
            .CreateAsync(new CreateItemCommand($"Mug-{Guid.NewGuid():N}", "", 10m, stock, Publish: true)));

    private static T Value<T>(Result<T> result) =>
        result.Match(v => v, e => throw new ShouldAssertException($"{e.Code}: {e.Message}"));

    [Fact]
    public async Task Migrations_shard_the_shop_tables()
    {
        var tables = await InScope(async sp =>
        {
            var db = sp.GetRequiredService<ShopDbContext>();
            return await db.Database
                .SqlQuery<CitusTable>($"SELECT table_name::text AS \"Name\", citus_table_type AS \"Type\", distribution_column AS \"Column\" FROM citus_tables")
                .ToListAsync();
        });

        tables.ShouldContain(new CitusTable("\"Items\"", "reference", "<none>"));
        tables.ShouldContain(new CitusTable("\"Carts\"", "distributed", "UserId"));
        tables.ShouldContain(new CitusTable("\"CartLines\"", "distributed", "UserId"));
        tables.ShouldContain(new CitusTable("\"Orders\"", "distributed", "UserId"));
        tables.ShouldContain(new CitusTable("\"OrderLines\"", "distributed", "UserId"));
        tables.ShouldContain(new CitusTable("\"StockMovements\"", "distributed", "ItemId"));
    }

    [Fact]
    public async Task Audit_timestamps_are_set_on_insert_and_update()
    {
        var created = Value(await CreateItemAsync());
        created.CreatedAt.ShouldNotBe(default);
        created.UpdatedAt.ShouldBe(created.CreatedAt);

        await Task.Delay(20);
        var refilled = Value(await InScope(sp =>
            sp.GetRequiredService<IBackofficeItemService>().RefillAsync(new RefillItemCommand(created.Id, 1))));
        var reloaded = Value(await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().GetAsync(created.Id)));

        refilled.Stock.ShouldBe(6);
        reloaded.CreatedAt.ShouldBe(created.CreatedAt);
        reloaded.UpdatedAt.ShouldBeGreaterThan(created.UpdatedAt);
    }

    [Fact]
    public async Task Deleting_is_logical()
    {
        var item = Value(await CreateItemAsync());

        Value(await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().DeleteAsync(item.Id)));

        (await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().GetAsync(item.Id)))
            .Match(_ => "found", e => e.Code).ShouldBe("item.not_found");

        var row = await InScope(sp => sp.GetRequiredService<ShopDbContext>().Items
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(i => i.Id == item.Id));
        row.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Checkout_writes_across_the_reference_table_and_the_user_shard()
    {
        var userId = Guid.NewGuid();
        var item = Value(await CreateItemAsync(stock: 5));

        Value(await InScope(sp => sp.GetRequiredService<ICartService>().AddAsync(userId, item.Id, 2)));
        Value(await InScope(sp => sp.GetRequiredService<ICartService>().AddAsync(userId, item.Id, 1)));
        var order = Value(await InScope(sp => sp.GetRequiredService<ICheckoutService>().PlaceOrderAsync(userId)));

        order.Total.ShouldBe(30m);
        order.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        Value(await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().GetAsync(item.Id))).Stock.ShouldBe(2);
        (await InScope(sp => sp.GetRequiredService<ICartService>().GetAsync(userId))).Lines.ShouldBeEmpty();
        (await InScope(sp => sp.GetRequiredService<IOrderQueryService>().ListForUserAsync(userId))).ShouldHaveSingleItem().Id.ShouldBe(order.Id);
        (await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().ListMovementsAsync(item.Id)))
            .Select(m => m.Delta).ShouldBe([-3, 5], ignoreOrder: true);

        // The emptied cart lines are kept, flagged as deleted.
        var lines = await InScope(sp => sp.GetRequiredService<ShopDbContext>().CartLines
            .IgnoreQueryFilters().AsNoTracking().Where(l => l.UserId == userId).ToListAsync());
        lines.ShouldHaveSingleItem().IsDeleted.ShouldBeTrue();

        // A user can buy the same item again after checkout: the unique index ignores deleted lines.
        Value(await InScope(sp => sp.GetRequiredService<ICartService>().AddAsync(userId, item.Id, 1)));
    }

    [Fact]
    public async Task Concurrent_stock_changes_are_detected()
    {
        var item = Value(await CreateItemAsync(stock: 1));

        await using var slow = fixture.Scope();
        var result = await slow.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(async ct =>
        {
            var items = slow.ServiceProvider.GetRequiredService<IItemRepository>();
            var stale = (await items.GetAsync(item.Id, ct))!;

            // Another request commits first.
            Value(await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().RefillAsync(new RefillItemCommand(item.Id, 10))));

            return stale.Withdraw(new Quantity(1)).Map(withdrawn => { items.Update(withdrawn); return withdrawn; });
        });

        result.Match(_ => "committed", e => e.Kind.ToString()).ShouldBe(nameof(ErrorKind.Conflict));
        Value(await InScope(sp => sp.GetRequiredService<IBackofficeItemService>().GetAsync(item.Id))).Stock.ShouldBe(11);
    }

    public sealed record CitusTable(string Name, string Type, string Column);
}
