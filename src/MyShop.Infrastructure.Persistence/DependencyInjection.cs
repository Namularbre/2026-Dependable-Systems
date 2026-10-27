using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyShop.Application.Ports.Out;

namespace MyShop.Infrastructure.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Shop";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
                               ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' not found.");

        // The Identity tables' shape depends on these options: pin them here so every host
        // (web, migrator, tests) builds the same model as the migrations.
        services.Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<ShopDbContext>((sp, options) => options.Configure(connectionString, sp.GetRequiredService<AuditInterceptor>()));

        return services
            .AddScoped<IItemRepository, ItemRepository>()
            .AddScoped<IStockMovementRepository, StockMovementRepository>()
            .AddScoped<ICartRepository, CartRepository>()
            .AddScoped<IOrderRepository, OrderRepository>()
            .AddScoped<IUnitOfWork, UnitOfWork>();
    }

    public static DbContextOptionsBuilder Configure(this DbContextOptionsBuilder options, string connectionString, AuditInterceptor interceptor) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory"))
            .AddInterceptors(interceptor);
}
