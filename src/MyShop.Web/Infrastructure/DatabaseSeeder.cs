using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyShop.Application;
using MyShop.Application.Ports.In;
using MyShop.Infrastructure.Persistence;
using MyShop.Infrastructure.Persistence.Entities;

namespace MyShop.Web.Infrastructure;

public static partial class DatabaseSeeder
{
    /// <summary>Applies pending migrations, then seeds the admin account and a demo catalog. Idempotent.</summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSeeder));

        var db = services.GetRequiredService<ShopDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        LogMigrating(logger, pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();

        // Create the shared data-protection key now, so the replicas don't each race to create their own
        // on first start (a cookie protected by one replica's fresh key would be unreadable by the others).
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector("MyShop.KeyRing").Protect([0]);

        await SeedAdminAsync(services, app.Configuration, logger);
        await SeedCatalogAsync(services.GetRequiredService<IBackofficeItemService>(), logger);
    }

    private static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roles = services.GetRequiredService<RoleManager<ShopRole>>();
        if (!await roles.RoleExistsAsync(Roles.Admin))
            await roles.CreateAsync(new ShopRole { Name = Roles.Admin });

        var (email, password) = (config["Seed:AdminEmail"], config["Seed:AdminPassword"]);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = services.GetRequiredService<UserManager<ShopUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;

        var admin = new ShopUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            LogAdminFailed(logger, email, string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(admin, Roles.Admin);
        LogAdminCreated(logger, email);
    }

    private static async Task SeedCatalogAsync(IBackofficeItemService items, ILogger logger)
    {
        if ((await items.ListAsync()).Count > 0) return;

        CreateItemCommand[] demo =
        [
            new("Coffee mug", "A sturdy 350 ml ceramic mug.", 12.50m, 40, true),
            new("Notebook", "A5 dotted notebook, 120 pages.", 8.90m, 100, true),
            new("Mechanical pencil", "0.5 mm, aluminium body.", 6.00m, 25, true),
            new("Desk lamp", "LED lamp with adjustable arm.", 39.00m, 0, false),
        ];
        foreach (var command in demo) await items.CreateAsync(command);
        LogCatalogSeeded(logger, demo.Length);
    }

    [LoggerMessage(3000, LogLevel.Information, "Applying {Count} pending migration(s): {Migrations}")]
    private static partial void LogMigrating(ILogger logger, int count, string migrations);

    [LoggerMessage(3001, LogLevel.Information, "Admin account {Email} created")]
    private static partial void LogAdminCreated(ILogger logger, string email);

    [LoggerMessage(3002, LogLevel.Error, "Could not create admin account {Email}: {Errors}")]
    private static partial void LogAdminFailed(ILogger logger, string email, string errors);

    [LoggerMessage(3003, LogLevel.Information, "Seeded {Count} demo item(s)")]
    private static partial void LogCatalogSeeded(ILogger logger, int count);
}
