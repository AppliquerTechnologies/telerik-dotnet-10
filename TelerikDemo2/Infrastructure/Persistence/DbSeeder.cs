using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Domain;

namespace TelerikDemo2.Infrastructure.Persistence;

public static class DbSeeder
{
    private const int CustomerCount = 200;
    private const int OrderCount = 150_000;
    private const int BatchSize = 5_000;
    private const int ConnectAttempts = 12;

    // Fixed end date so the data is the same on every run.
    private static readonly DateTime EndDate = new(2026, 9, 30);

    private static readonly string[] FirstNames =
        { "Alex", "Sam", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Jamie", "Avery", "Quinn",
          "Drew", "Blake", "Cameron", "Devon", "Elliot", "Finley", "Harper", "Indigo", "Kai", "Logan" };
    private static readonly string[] LastNames =
        { "Smith", "Johnson", "Brown", "Garcia", "Miller", "Davis", "Wilson", "Anderson", "Thomas", "Moore",
          "Martin", "Lee", "Clark", "Lewis", "Walker", "Hall", "Young", "King", "Wright", "Scott" };
    private static readonly string[] Cities =
        { "Springfield", "Riverton", "Lakeside", "Fairview", "Georgetown", "Madison", "Clinton", "Franklin" };
    private static readonly string[] NoteSamples =
        { "Customer requested gift wrapping.", "Deliver to back entrance.", "Call before delivery.",
          "Fragile items - handle with care.", "Repeat customer, priority handling.",
          "Billing address differs from shipping address.", "Awaiting stock confirmation." };

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // EnsureCreated instead of migrations (see README). Retried because in Docker the app
        // can start before SQL Server accepts connections.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < ConnectAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, ConnectAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        // Seed on first startup only.
        if (await db.Customers.AnyAsync(ct))
        {
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rnd = new Random(20260930);

        var customers = new List<Customer>(CustomerCount);
        for (var i = 1; i <= CustomerCount; i++)
        {
            var first = FirstNames[rnd.Next(FirstNames.Length)];
            var last = LastNames[rnd.Next(LastNames.Length)];
            customers.Add(new Customer
            {
                Name = $"{first} {last} #{i}",
                Email = $"{first}.{last}{i}@example.test".ToLowerInvariant(),
                City = Cities[rnd.Next(Cities.Length)]
            });
        }
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);
        var customerIds = customers.Select(c => c.Id).ToArray();
        db.ChangeTracker.Clear();

        var rangeDays = (EndDate - EndDate.AddYears(-3)).Days;
        var startDate = EndDate.AddYears(-3);
        var batch = new List<Order>(BatchSize);

        for (var i = 0; i < OrderCount; i++)
        {
            var roll = rnd.Next(100);
            var status = roll < 20 ? OrderStatus.New
                       : roll < 45 ? OrderStatus.Processing
                       : roll < 90 ? OrderStatus.Shipped
                       : OrderStatus.Cancelled;

            batch.Add(new Order
            {
                CustomerId = customerIds[rnd.Next(customerIds.Length)],
                OrderDate = startDate.AddDays(rnd.Next(rangeDays + 1)),
                Total = Math.Round(OrderRules.MinTotal + (decimal)rnd.NextDouble() * (OrderRules.MaxTotal - OrderRules.MinTotal), 2),
                Status = status,
                Notes = rnd.Next(100) < 30 ? NoteSamples[rnd.Next(NoteSamples.Length)] : null
            });

            if (batch.Count == BatchSize)
            {
                await FlushAsync(db, batch, ct);
                logger.LogInformation("Seeded {Count}/{Total} orders", i + 1, OrderCount);
            }
        }
        if (batch.Count > 0)
        {
            await FlushAsync(db, batch, ct);
        }

        logger.LogInformation("Seeding finished in {Seconds:F1}s", sw.Elapsed.TotalSeconds);
    }

    private static async Task FlushAsync(AppDbContext db, List<Order> batch, CancellationToken ct)
    {
        db.Orders.AddRange(batch);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        batch.Clear();
    }
}
