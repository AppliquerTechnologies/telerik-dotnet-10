using Kendo.Mvc;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Mvc;
using TelerikDemo2.Common;

namespace TelerikDemo2.Features.Orders;

// Offset paging with the standard Telerik pager.
public class IndexModel(IDispatcher dispatcher) : OrdersPageModelBase(dispatcher)
{
    public async Task<IActionResult> OnPostReadAsync([DataSourceRequest] DataSourceRequest request, string search, int? customerId)
    {
        // Tie-break on the key so pages are stable when the sort column has duplicates.
        request.Sorts ??= new List<SortDescriptor>();
        if (!request.Sorts.Any(s => s.Member == nameof(OrderViewModel.OrderId)))
        {
            request.Sorts.Add(new SortDescriptor(nameof(OrderViewModel.OrderId), ListSortDirection.Descending));
        }

        var query = await Dispatcher.SendAsync(new ListOrders(search, customerId), Ct);
        return new JsonResult(await query.ToDataSourceResultAsync(request));
    }
}
