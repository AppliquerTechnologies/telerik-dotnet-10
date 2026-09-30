using Kendo.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Features.Auth;
using TelerikDemo2.Features.Orders;
using TelerikDemo2.Infrastructure;
using TelerikDemo2.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Without persisted keys everyone is signed out whenever the container restarts.
// compose mounts a volume at DataProtection:KeysPath.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("TelerikDemo2")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddRequestHandlers(typeof(Program).Assembly)
    .AddScoped<OrderInputValidator>()
    .AddDemoAuthentication();

// Pages live in /Features instead of /Pages. Telerik expects PascalCase JSON.
builder.Services.AddRazorPages(options => options.RootDirectory = "/Features")
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

// Scripts stay inline (not deferred) so widgets in the AJAX-loaded details window initialise.
builder.Services.AddKendo();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

await DbSeeder.SeedAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Switched off in the container image; TLS is expected to end at a reverse proxy.
if (app.Configuration.GetValue("UseHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapRazorPages();
app.MapOrdersApi();

// Health probe: anonymous, only reports whether the database is reachable.
app.MapGet("/healthz", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct) ? Results.Ok("healthy") : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
   .AllowAnonymous();

app.Run();
