using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyShop.Infrastructure.Persistence.Entities;

namespace MyShop.Infrastructure.Persistence;

/// <summary>
/// Stamps CreatedAt/UpdatedAt, bumps optimistic-concurrency versions and turns every
/// physical delete into a logical one, so no row ever leaves the database.
/// </summary>
public sealed class AuditInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = time.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % 10)); // PostgreSQL stores microseconds: keep tracked and stored values equal

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }

            if (entry is { State: EntityState.Modified, Entity: IVersioned versioned })
                versioned.Version++;
        }
    }
}
