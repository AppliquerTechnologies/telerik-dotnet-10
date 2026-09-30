using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Customers;

public sealed record GetCustomerLookup : IRequest<IReadOnlyList<CustomerLookupViewModel>>;

public class CustomerLookupViewModel
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public sealed class GetCustomerLookupHandler(AppDbContext db)
    : IRequestHandler<GetCustomerLookup, IReadOnlyList<CustomerLookupViewModel>>
{
    public async Task<IReadOnlyList<CustomerLookupViewModel>> HandleAsync(GetCustomerLookup request, CancellationToken ct) =>
        await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerLookupViewModel { Id = c.Id, Name = c.Name })
            .ToListAsync(ct);
}
