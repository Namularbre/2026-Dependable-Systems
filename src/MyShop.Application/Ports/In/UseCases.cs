using MyShop.Domain.Common;

namespace MyShop.Application.Ports.In;

// Driving ports, called by the web adapter.

public interface ICatalogService
{
    Task<IReadOnlyList<ItemDto>> ListAvailableAsync(CancellationToken ct = default);
    Task<Result<ItemDto>> GetAsync(Guid id, CancellationToken ct = default);
}

public interface ICartService
{
    Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<CartDto>> AddAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct = default);
    Task<Result<CartDto>> ChangeQuantityAsync(Guid userId, Guid itemId, int quantity, CancellationToken ct = default);
    Task<Result<CartDto>> RemoveAsync(Guid userId, Guid itemId, CancellationToken ct = default);
}

public interface ICheckoutService
{
    Task<Result<OrderDto>> PlaceOrderAsync(Guid userId, CancellationToken ct = default);
}

public interface IOrderQueryService
{
    Task<IReadOnlyList<OrderDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> ListAllAsync(CancellationToken ct = default);
}

public interface IBackofficeItemService
{
    Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken ct = default);
    Task<Result<ItemDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(Guid id, CancellationToken ct = default);
    Task<Result<ItemDto>> CreateAsync(CreateItemCommand command, CancellationToken ct = default);
    Task<Result<ItemDto>> UpdateAsync(UpdateItemCommand command, CancellationToken ct = default);
    Task<Result<ItemDto>> RefillAsync(RefillItemCommand command, CancellationToken ct = default);
    Task<Result<ItemDto>> SetPublishedAsync(Guid id, bool published, CancellationToken ct = default);
    Task<Result<Unit>> DeleteAsync(Guid id, CancellationToken ct = default);
}
