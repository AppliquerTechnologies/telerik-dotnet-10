using TelerikDemo2.Domain;

namespace TelerikDemo2.Features.Orders;

// Search and customer filter, used by both list queries.
public static class OrderFilters
{
    public static IQueryable<Order> Apply(IQueryable<Order> query, string search, int? customerId)
    {
        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o => o.Customer.Name.Contains(term) || (o.Notes != null && o.Notes.Contains(term)));
        }
        return query;
    }
}
