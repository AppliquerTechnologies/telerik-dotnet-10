using Kendo.Mvc;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Mvc;
using TelerikDemo2.Common;

namespace TelerikDemo2.Features.Orders.Cursor;

// Cursor (keyset) paging. See Orders/Index for the offset version.
public class IndexModel(IDispatcher dispatcher) : OrdersPageModelBase(dispatcher)
{
    public async Task<IActionResult> OnPostReadAsync(
        [DataSourceRequest] DataSourceRequest request, string search, int? customerId, string cursor, int? pageSize)
    {
        var sort = request.Sorts?.FirstOrDefault();
        var filters = request.Filters;

        var page = await Dispatcher.SendAsync(new GetOrdersPage(
            search, customerId,
            q => filters is { Count: > 0 } ? (IQueryable<OrderViewModel>)q.Where(filters) : q,
            sort?.Member, sort?.SortDirection == ListSortDirection.Descending,
            cursor, pageSize ?? 0), Ct);

        // Total is only the rows on this page; counting everything is what cursor paging avoids.
        return new JsonResult(new { Data = page.Items, Total = page.Items.Count, page.NextCursor });
    }
}
