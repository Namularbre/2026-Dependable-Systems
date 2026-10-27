using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using MyShop.Application;
using MyShop.Infrastructure.Persistence;
using MyShop.Infrastructure.Persistence.Entities;
using MyShop.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o =>
{
    o.IncludeScopes = true;
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// Hexagon: application core + driven adapter. The web layer itself is the driving adapter.
builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration);

// Stateless: auth cookies and antiforgery tokens are protected with keys shared through the database,
// TempData uses the cookie provider and there is no session, so any replica can serve any request.
builder.Services.AddDataProtection()
    .SetApplicationName("MyShop")
    .PersistKeysToDbContext<ShopDbContext>();

builder.Services
    .AddDefaultIdentity<ShopUser>(o =>
    {
        o.SignIn.RequireConfirmedAccount = false;
        o.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<ShopRole>()
    .AddEntityFrameworkStores<ShopDbContext>();

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddRazorPages();

builder.Services.AddHttpLogging(o => o.LoggingFields =
    HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath |
    HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration);

builder.Services.AddHealthChecks().AddDbContextCheck<ShopDbContext>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear(); // trust the compose load balancer
    o.KnownProxies.Clear();
});

builder.Services.AddSingleton(new InstanceInfo(builder.Configuration["INSTANCE_NAME"] ?? Environment.MachineName));

var app = builder.Build();

// One-shot migration mode (the compose "migrator" service). Configurable through the environment too,
// because IDE container tooling (e.g. Rider's fast mode) replaces the container command line.
if (args.Contains("--migrate") || app.Configuration.GetValue<bool>("Database:MigrateAndExit"))
{
    await app.MigrateAndSeedAsync();
    return;
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.MigrateAndSeedAsync();

app.UseForwardedHeaders();
// Euro prices with a '.' decimal separator, so <input type="number"> values bind correctly.
app.UseRequestLocalization(o => o.AddSupportedCultures("en-IE").AddSupportedUICultures("en-IE").SetDefaultCulture("en-IE"));
app.UseInstanceTagging();
app.UseHttpLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Items}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Catalog}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
