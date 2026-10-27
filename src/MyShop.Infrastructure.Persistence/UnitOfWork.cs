using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyShop.Application.Ports.Out;
using MyShop.Domain.Common;

namespace MyShop.Infrastructure.Persistence;

internal sealed partial class UnitOfWork(ShopDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private static readonly Error ConcurrencyConflict =
        Error.Conflict("concurrency", "The data was changed by someone else. Please retry.");

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Citus forbids writing a reference table (Items) after parallel shard work in the same
        // transaction; sequential mode lifts that. Plain PostgreSQL accepts it as a no-op custom setting.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL citus.multi_shard_modify_mode TO 'sequential'", ct);
        try
        {
            var result = await work(ct);
            if (result.IsSuccess)
            {
                var written = await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                LogCommitted(written);
            }
            else
            {
                await RollbackAsync(tx, ct);
            }
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            LogConcurrencyConflict(ex.Entries.Select(e => e.Metadata.ClrType.Name).FirstOrDefault() ?? "?");
            await RollbackAsync(tx, ct);
            return ConcurrencyConflict;
        }
    }

    private async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        await tx.RollbackAsync(ct);
        db.ChangeTracker.Clear();
    }

    [LoggerMessage(2000, LogLevel.Debug, "Unit of work committed {Rows} row(s)")]
    private partial void LogCommitted(int rows);

    [LoggerMessage(2001, LogLevel.Warning, "Optimistic concurrency conflict on {Entity}; transaction rolled back")]
    private partial void LogConcurrencyConflict(string entity);
}
