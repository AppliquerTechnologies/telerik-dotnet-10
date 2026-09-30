using Kendo.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TelerikDemo2.Common;
using TelerikDemo2.Features.Auth;
using TelerikDemo2.Features.Orders;
using TelerikDemo2.Infrastructure;
using TelerikDemo2.Infrastructure.Persistence;

// Minimal logger so startup failures (bad configuration, database down) are logged as JSON too.
// It is replaced by the configured logger below once the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

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

    await DbInitializer.InitializeAsync(app.Services, app.Configuration);

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

    // One structured log line per request (after static files, so those are not logged).
    app.UseSerilogRequestLogging(options =>
    {
        // Health probes run every few seconds; keep them out of the normal log.
        options.GetLevel = (httpContext, _, ex) =>
            ex is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/healthz") ? LogEventLevel.Verbose
            : LogEventLevel.Information;

        options.EnrichDiagnosticContext = (diagnostics, httpContext) =>
        {
            diagnostics.Set("User", httpContext.User.Identity?.IsAuthenticated == true ? httpContext.User.Identity.Name : "anonymous");
            diagnostics.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
        };
    });

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application stopped during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
