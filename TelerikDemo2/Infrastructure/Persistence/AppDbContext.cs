using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Domain;

namespace TelerikDemo2.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Email).HasMaxLength(150);
            e.Property(c => c.City).HasMaxLength(80);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.Total).HasPrecision(18, 2);
            e.Property(o => o.Notes).HasMaxLength(OrderRules.NotesMaxLength);
            e.HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId);
            e.HasIndex(o => o.OrderDate);
            e.HasIndex(o => o.Status);
        });
    }
}
