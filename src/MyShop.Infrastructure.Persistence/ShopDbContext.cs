using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyShop.Domain.Items;
using MyShop.Infrastructure.Persistence.Entities;

namespace MyShop.Infrastructure.Persistence;

/// <summary>
/// Single context for the shop, Identity and data-protection keys.
/// Shop tables are sharded with Citus (see the AddCitusDistribution migration);
/// Identity and key tables stay local to the coordinator, which is the only node the app talks to.
/// </summary>
public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options)
    : IdentityDbContext<ShopUser, ShopRole, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<ItemEntity> Items => Set<ItemEntity>();
    public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();
    public DbSet<CartEntity> Carts => Set<CartEntity>();
    public DbSet<CartLineEntity> CartLines => Set<CartLineEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderLineEntity> OrderLines => Set<OrderLineEntity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ItemEntity>(e =>
        {
            e.ToTable("Items");
            e.Property(i => i.Name).HasMaxLength(Item.NameMaxLength);
            e.Property(i => i.Description).HasMaxLength(Item.DescriptionMaxLength);
            e.Property(i => i.Price).HasPrecision(12, 2);
            e.Property(i => i.Version).IsConcurrencyToken();
            e.HasIndex(i => i.IsPublished);
        });

        builder.Entity<StockMovementEntity>(e =>
        {
            e.ToTable("StockMovements");
            e.HasKey(m => new { m.ItemId, m.Id }); // shard key must be part of the primary key
            e.Property(m => m.Reason).HasMaxLength(32);
            e.HasOne<ItemEntity>().WithMany().HasForeignKey(m => m.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CartEntity>(e =>
        {
            e.ToTable("Carts");
            e.HasKey(c => new { c.UserId, c.Id });
            e.HasIndex(c => c.UserId).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasMany(c => c.Lines).WithOne()
                .HasForeignKey(l => new { l.UserId, l.CartId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CartLineEntity>(e =>
        {
            e.ToTable("CartLines");
            e.HasKey(l => new { l.UserId, l.Id });
            e.HasIndex(l => new { l.UserId, l.CartId, l.ItemId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne<ItemEntity>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderEntity>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(o => new { o.UserId, o.Id });
            e.Property(o => o.Total).HasPrecision(12, 2);
            e.HasIndex(o => new { o.UserId, o.PlacedAt });
            e.HasMany(o => o.Lines).WithOne()
                .HasForeignKey(l => new { l.UserId, l.OrderId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderLineEntity>(e =>
        {
            e.ToTable("OrderLines");
            e.HasKey(l => new { l.UserId, l.Id });
            e.Property(l => l.ItemName).HasMaxLength(Item.NameMaxLength);
            e.Property(l => l.UnitPrice).HasPrecision(12, 2);
            e.HasOne<ItemEntity>().WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        ApplySoftDeleteFilters(builder);
    }

    /// <summary>Every auditable entity is hidden once logically deleted. Use IgnoreQueryFilters() to see it.</summary>
    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var type in builder.Model.GetEntityTypes().Where(t => typeof(IAuditable).IsAssignableFrom(t.ClrType)))
        {
            var e = Expression.Parameter(type.ClrType, "e");
            var notDeleted = Expression.Lambda(
                Expression.Not(Expression.Property(e, nameof(IAuditable.IsDeleted))), e);
            builder.Entity(type.ClrType).HasQueryFilter(notDeleted);
            builder.Entity(type.ClrType).HasIndex(nameof(IAuditable.IsDeleted));
        }
    }
}
