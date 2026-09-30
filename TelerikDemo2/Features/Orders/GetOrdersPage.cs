using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

// Cursor-paged list (see ListOrders for the offset version).
// Refine lets the page add Telerik's column filters without this handler referencing Telerik.
public sealed record GetOrdersPage(
    string Search,
    int? CustomerId,
    Func<IQueryable<OrderViewModel>, IQueryable<OrderViewModel>> Refine,
    string SortField,
    bool Descending,
    string Cursor,
    int PageSize) : IRequest<CursorPage<OrderViewModel>>;

public sealed class GetOrdersPageHandler(AppDbContext db) : IRequestHandler<GetOrdersPage, CursorPage<OrderViewModel>>
{
    // Anything else falls back to newest first.
    private static readonly string[] SortableFields =
    {
        nameof(OrderViewModel.OrderId), nameof(OrderViewModel.CustomerName), nameof(OrderViewModel.OrderDate),
        nameof(OrderViewModel.Total), nameof(OrderViewModel.Status)
    };

    public Task<CursorPage<OrderViewModel>> HandleAsync(GetOrdersPage request, CancellationToken ct)
    {
        var projected = OrderFilters.Apply(db.Orders.AsNoTracking(), request.Search, request.CustomerId).ToViewModel();
        if (request.Refine is not null)
        {
            projected = request.Refine(projected);
        }

        var sortField = SortableFields.FirstOrDefault(f => string.Equals(f, request.SortField, StringComparison.OrdinalIgnoreCase));
        var descending = sortField is null || request.Descending;

        return KeysetPager.ToPageAsync(
            projected, sortField ?? nameof(OrderViewModel.OrderId), nameof(OrderViewModel.OrderId),
            descending, request.Cursor, request.PageSize, ct);
    }
}
