using Microsoft.Extensions.Logging;
using MyShop.Domain.Common;

namespace MyShop.Application;

/// <summary>Source-generated, allocation-free log messages for the use cases.</summary>
internal static partial class Log
{
    [LoggerMessage(1000, LogLevel.Information, "Item {ItemId} '{Name}' created with stock {Stock}")]
    public static partial void ItemCreated(this ILogger logger, Guid itemId, string name, int stock);

    [LoggerMessage(1001, LogLevel.Information, "Item {ItemId} updated")]
    public static partial void ItemUpdated(this ILogger logger, Guid itemId);

    [LoggerMessage(1002, LogLevel.Information, "Item {ItemId} refilled by {Quantity}, stock is now {Stock}")]
    public static partial void ItemRefilled(this ILogger logger, Guid itemId, int quantity, int stock);

    [LoggerMessage(1003, LogLevel.Information, "Item {ItemId} published={Published}")]
    public static partial void ItemPublicationChanged(this ILogger logger, Guid itemId, bool published);

    [LoggerMessage(1004, LogLevel.Information, "Item {ItemId} logically deleted")]
    public static partial void ItemDeleted(this ILogger logger, Guid itemId);

    [LoggerMessage(1100, LogLevel.Debug, "Cart of user {UserId} now holds {Count} unit(s)")]
    public static partial void CartChanged(this ILogger logger, Guid userId, int count);

    [LoggerMessage(1200, LogLevel.Information, "Order {OrderId} placed by user {UserId} for {Total}")]
    public static partial void OrderPlaced(this ILogger logger, Guid orderId, Guid userId, decimal total);

    [LoggerMessage(1201, LogLevel.Warning, "Checkout failed for user {UserId}: {ErrorCode} {ErrorMessage}")]
    public static partial void CheckoutFailed(this ILogger logger, Guid userId, string errorCode, string errorMessage);

    [LoggerMessage(1900, LogLevel.Warning, "Use case {UseCase} rejected: {ErrorCode} {ErrorMessage}")]
    public static partial void UseCaseRejected(this ILogger logger, string useCase, string errorCode, string errorMessage);

    /// <summary>Logs the outcome of a use case and passes the result through unchanged.</summary>
    public static Result<T> LogOutcome<T>(this Result<T> result, ILogger logger, string useCase, Action<ILogger, T> onSuccess) =>
        result.Match(
            value => { onSuccess(logger, value); return result; },
            error => { logger.UseCaseRejected(useCase, error.Code, error.Message); return result; });

    public static async Task<Result<T>> LogOutcome<T>(this Task<Result<T>> result, ILogger logger, string useCase, Action<ILogger, T> onSuccess) =>
        (await result).LogOutcome(logger, useCase, onSuccess);
}
