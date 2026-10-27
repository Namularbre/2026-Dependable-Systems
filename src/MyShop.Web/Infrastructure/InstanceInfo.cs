namespace MyShop.Web.Infrastructure;

/// <summary>Name of this replica, so load-balanced requests can be traced to the instance that served them.</summary>
public sealed record InstanceInfo(string Name);

public static class InstanceTaggingExtensions
{
    /// <summary>Adds an "Instance" logging scope and an X-Served-By response header to every request.</summary>
    public static IApplicationBuilder UseInstanceTagging(this WebApplication app)
    {
        var instance = app.Services.GetRequiredService<InstanceInfo>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MyShop.Web.Instance");

        return app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Served-By"] = instance.Name;
            using (logger.BeginScope("Instance:{Instance}", instance.Name))
                await next(context);
        });
    }
}
