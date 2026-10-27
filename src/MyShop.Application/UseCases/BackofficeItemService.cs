using Microsoft.Extensions.Logging;
using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Application.UseCases;

public sealed class BackofficeItemService(
    IItemRepository items,
    IStockMovementRepository movements,
    IUnitOfWork uow,
    ILogger<BackofficeItemService> logger) : IBackofficeItemService
{
    public async Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken ct = default) =>
        [.. (await items.ListAsync(publishedOnly: false, ct)).Select(ItemDto.From)];

    public Task<Result<ItemDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        items.FindAsync(id, ct).Map(ItemDto.From);

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(Guid id, CancellationToken ct = default) =>
        [.. (await movements.ListByItemAsync(id, ct)).Select(StockMovementDto.From)];

    public Task<Result<ItemDto>> CreateAsync(CreateItemCommand command, CancellationToken ct = default) =>
        uow.ExecuteAsync(_ => Task.FromResult(
                    from created in Item.Create(command.Name, command.Description, command.Price)
                    from stocked in command.InitialStock switch
                    {
                        < 0 => Result.Fail<Item>(Error.Validation("item.negative_stock", "Initial stock cannot be negative.")),
                        0 => Result.Ok(created),
                        var n => created.Refill(new Quantity(n))
                    }
                    from published in command.Publish ? stocked.Publish() : stocked
                    select Persist(published, command.InitialStock)), ct)
            .BindAsync(item => GetAsync(item.Id, ct))
            .LogOutcome(logger, nameof(CreateAsync), (log, i) => log.ItemCreated(i.Id, i.Name, i.Stock));

    public Task<Result<ItemDto>> UpdateAsync(UpdateItemCommand command, CancellationToken ct = default) =>
        Modify(command.Id, i => i.Update(command.Name, command.Description, command.Price), ct)
            .BindAsync(item => GetAsync(item.Id, ct))
            .LogOutcome(logger, nameof(UpdateAsync), (log, i) => log.ItemUpdated(i.Id));

    public Task<Result<ItemDto>> RefillAsync(RefillItemCommand command, CancellationToken ct = default) =>
        Quantity.Create(command.Quantity)
            .BindAsync(q => Modify(command.Id, i => i.Refill(q), ct, StockMovement.Refill(command.Id, q)))
            .BindAsync(item => GetAsync(item.Id, ct))
            .LogOutcome(logger, nameof(RefillAsync), (log, i) => log.ItemRefilled(i.Id, command.Quantity, i.Stock));

    public Task<Result<ItemDto>> SetPublishedAsync(Guid id, bool published, CancellationToken ct = default) =>
        Modify(id, i => published ? i.Publish() : i.Unpublish(), ct)
            .BindAsync(item => GetAsync(item.Id, ct))
            .LogOutcome(logger, nameof(SetPublishedAsync), (log, i) => log.ItemPublicationChanged(i.Id, i.IsPublished));

    public Task<Result<Unit>> DeleteAsync(Guid id, CancellationToken ct = default) =>
        Modify(id, i => i.Delete(), ct)
            .Map(_ => Unit.Value)
            .LogOutcome(logger, nameof(DeleteAsync), (log, _) => log.ItemDeleted(id));

    private Item Persist(Item item, int initialStock)
    {
        items.Add(item);
        if (initialStock > 0) movements.Add(StockMovement.Refill(item.Id, new Quantity(initialStock)));
        return item;
    }

    // Writes return the domain record; callers reload it after commit so the DTO carries
    // the audit timestamps stamped by the persistence adapter.
    private Task<Result<Item>> Modify(Guid id, Func<Item, Result<Item>> change, CancellationToken ct, StockMovement? movement = null) =>
        uow.ExecuteAsync(token =>
            items.FindAsync(id, token)
                .Bind(change)
                .Map(updated =>
                {
                    items.Update(updated);
                    if (movement is not null) movements.Add(movement);
                    return updated;
                }), ct);
}
