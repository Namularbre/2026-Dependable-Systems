using MyShop.Application.Ports.In;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Common;

namespace MyShop.Application.UseCases;

public sealed class CatalogService(IItemRepository items) : ICatalogService
{
    public async Task<IReadOnlyList<ItemDto>> ListAvailableAsync(CancellationToken ct = default) =>
        [.. (await items.ListAsync(publishedOnly: true, ct)).Select(ItemDto.From)];

    public Task<Result<ItemDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        items.FindAsync(id, ct)
            .Bind(i => i.EnsureSellable())
            .Map(ItemDto.From);
}
