using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

// Loads what OrderRules needs (customer lookup, current date) and applies it. Used by create and update.
public sealed class OrderInputValidator(AppDbContext db, TimeProvider clock)
{
    public async Task ValidateAsync(OrderViewModel model, Result result, CancellationToken ct)
    {
        var customerExists = model.CustomerId is not null
                             && await db.Customers.AnyAsync(c => c.Id == model.CustomerId, ct);

        foreach (var violation in OrderRules.CheckDetails(customerExists, model.OrderDate, model.Total, clock.GetLocalNow().DateTime))
        {
            result.AddViolation(violation);
        }
    }
}
