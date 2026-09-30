using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

// Offset-paged list. Returns an unexecuted IQueryable so Telerik's ToDataSourceResultAsync can apply
// paging, sorting and filtering in SQL (see GetOrdersPage for the cursor version).
public sealed record ListOrders(string Search, int? CustomerId) : IRequest<IQueryable<OrderViewModel>>;

public sealed class ListOrdersHandler(AppDbContext db) : IRequestHandler<ListOrders, IQueryable<OrderViewModel>>
{
    public Task<IQueryable<OrderViewModel>> HandleAsync(ListOrders request, CancellationToken ct) =>
        Task.FromResult(OrderFilters.Apply(db.Orders.AsNoTracking(), request.Search, request.CustomerId).ToViewModel());
}
