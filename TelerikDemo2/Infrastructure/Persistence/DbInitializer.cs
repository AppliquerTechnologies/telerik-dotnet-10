using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TelerikDemo2.Infrastructure.Persistence;

// Runs at startup: applies pending EF Core migrations, then seeds an empty database.
// Set Database:MigrateOnStartup to false to apply migrations some other way (dotnet ef database update,
// a SQL script or a deployment step); the app then assumes the schema is already up to date.
public static class DbInitializer
{
    private const int MaxAttempts = 12;

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        if (configuration.GetValue("Database:MigrateOnStartup", true))
        {
            await MigrateWithRetryAsync(db, logger, ct);
        }

        await DbSeeder.SeedAsync(services, ct);
    }

    // In Docker the app can start before SQL Server accepts connections, so retry connection-type
    // errors for a while. Anything else (for example an old schema) fails straight away.
    private static async Task MigrateWithRetryAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
                if (pending.Count > 0)
                {
                    logger.LogInformation("Applying {Count} migration(s): {Names}", pending.Count, string.Join(", ", pending));
                }
                await db.Database.MigrateAsync(ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsConnectionProblem(ex))
            {
                logger.LogWarning("Database not reachable yet (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private static bool IsConnectionProblem(Exception ex) =>
        ex is SqlException { IsTransient: true } || ex.InnerException is SqlException { IsTransient: true };
}
