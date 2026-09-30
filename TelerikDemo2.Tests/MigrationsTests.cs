using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Infrastructure.Persistence;
using Xunit;

namespace TelerikDemo2.Tests;

public class MigrationsTests
{
    // Fails when the model was changed without adding a migration. No database is contacted.
    [Fact]
    public void Model_has_no_pending_changes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;

        using var db = new AppDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF model differs from the latest migration. Run: dotnet ef migrations add <Name> --project TelerikDemo2");
    }
}
