using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyShop.Application;
using MyShop.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MyShop.Infrastructure.Tests;

/// <summary>Single-node Citus (the coordinator also holds the shards) with every migration applied.</summary>
public sealed class CitusFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("citusdata/citus:13.0").Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{Persistence.DependencyInjection.ConnectionStringName}"] = _container.GetConnectionString()
            })
            .Build();

        Services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddPersistence(configuration)
            .BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Each scope is a fresh DbContext, like one HTTP request.</summary>
    public AsyncServiceScope Scope() => Services.CreateAsyncScope();
}

[CollectionDefinition(Name)]
public sealed class CitusCollection : ICollectionFixture<CitusFixture>
{
    public const string Name = "citus";
}
