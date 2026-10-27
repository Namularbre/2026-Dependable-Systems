using Microsoft.Extensions.DependencyInjection;
using MyShop.Application.Ports.In;
using MyShop.Application.UseCases;

namespace MyShop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddSingleton(TimeProvider.System)
            .AddScoped<ICatalogService, CatalogService>()
            .AddScoped<ICartService, CartService>()
            .AddScoped<ICheckoutService, CheckoutService>()
            .AddScoped<IOrderQueryService, OrderQueryService>()
            .AddScoped<IBackofficeItemService, BackofficeItemService>();
}
