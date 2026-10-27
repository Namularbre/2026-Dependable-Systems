using MyShop.Application.Ports.Out;
using MyShop.Domain.Common;
using MyShop.Domain.Items;

namespace MyShop.Application;

internal static class Errors
{
    public static readonly Error ItemNotFound = Error.NotFound("item.not_found", "Item not found.");

    public static async Task<Result<Item>> FindAsync(this IItemRepository items, Guid id, CancellationToken ct) =>
        Result.FromNullable(await items.GetAsync(id, ct), ItemNotFound);
}
