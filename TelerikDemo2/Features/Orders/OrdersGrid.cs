using Kendo.Mvc.UI;
using Kendo.Mvc.UI.Fluent;
using Microsoft.AspNetCore.Mvc;
using TelerikDemo2.Domain;

namespace TelerikDemo2.Features.Orders;

// Grid setup shared by both Orders pages.
public static class OrdersGrid
{
    private const string EmptyTemplate =
        "<div class='empty'><svg width='44' height='44' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'>" +
        "<circle cx='11' cy='11' r='7'/><path d='m21 21-4.3-4.3'/></svg><strong>No orders found</strong>Try a different search, customer or filter.</div>";

    public static GridBuilder<OrderViewModel> ConfigureOrders(this GridBuilder<OrderViewModel> grid, bool canManage)
    {
        var statuses = StatusItem.All();

        return grid
            .Columns(columns =>
            {
                columns.Bound(c => c.OrderId).Title("Order").Width(100).HtmlAttributes(new { @class = "col-id" });
                columns.Bound(c => c.CustomerName).Title("Customer").HtmlAttributes(new { @class = "col-customer" });
                columns.Bound(c => c.OrderDate).Title("Date").Format("{0:dd MMM yyyy}").Width(150).HtmlAttributes(new { @class = "col-date" });
                columns.Bound(c => c.Total).Format("{0:C}").Width(140)
                    .HtmlAttributes(new { @class = "col-money" })
                    .HeaderHtmlAttributes(new { style = "text-align:right" });
                columns.ForeignKey(c => c.Status, statuses, "Value", "Text").Width(160)
                    .ClientTemplate("<span class='status-badge status-#=Status#'>#=statusName(Status)#</span>");
                columns.Command(command =>
                {
                    command.Custom("Details").Text("Details").Click("showDetails");
                    if (canManage)
                    {
                        command.Edit();
                        command.Destroy();
                    }
                }).Width(canManage ? 270 : 130).HtmlAttributes(new { @class = "col-actions" });
            })
            .ToolBar(toolbar =>
            {
                if (canManage)
                {
                    toolbar.Create().Text("New order");
                }
            })
            .Editable(editable => editable.Mode(GridEditMode.PopUp).TemplateName("OrderEditor").DisplayDeleteConfirmation(false).Window(w => w.Title("Order").Width(520)))
            .Events(e => e.Remove("onRemove"))   // themed confirm instead of window.confirm
            .Sortable()
            .Filterable()
            .NoRecords(n => n.Template(EmptyTemplate))
            .Scrollable(s => s.Height(600));
    }

    // Everything except Read and paging, which differ per page.
    public static void ConfigureOrdersDataSource(this AjaxDataSourceBuilder<OrderViewModel> ds, IUrlHelper url)
    {
        ds.ServerOperation(true)
          .Sort(sort => sort.Add(c => c.OrderDate).Descending())
          .Model(m =>
          {
              m.Id(c => c.OrderId);
              m.Field(c => c.CustomerName).Editable(false);
              m.Field(c => c.Status).DefaultValue((int)OrderRules.InitialStatus);
          })
          .Create(r => r.Url(url.Page(null, "Create")).Data("forgeryToken"))
          .Update(r => r.Url(url.Page(null, "Update")).Data("forgeryToken"))
          .Destroy(r => r.Url(url.Page(null, "Destroy")).Data("forgeryToken"));
    }
}
