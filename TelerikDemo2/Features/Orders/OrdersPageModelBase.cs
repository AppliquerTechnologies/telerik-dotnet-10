using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelerikDemo2.Common;
using TelerikDemo2.Features.Auth;
using TelerikDemo2.Features.Customers;

namespace TelerikDemo2.Features.Orders;

// Handlers shared by /Orders (offset paging) and /Orders/Cursor (cursor paging); the two pages
// only differ in how they read the list. Each handler binds input, checks the permission,
// sends one request and shapes the response for Telerik.
public abstract class OrdersPageModelBase : PageModel
{
    private const string LastViewedKey = "LastViewedOrderId";

    protected OrdersPageModelBase(IDispatcher dispatcher) => Dispatcher = dispatcher;

    protected IDispatcher Dispatcher { get; }
    protected CancellationToken Ct => HttpContext.RequestAborted;

    public bool CanManage => User.CanManageOrders();

    // Kept in session: the last order opened in the details window.
    public int? LastViewedOrderId => HttpContext.Session.GetInt32(LastViewedKey);

    public void OnGet() { }

    public async Task<IActionResult> OnPostCustomersAsync() =>
        new JsonResult(await Dispatcher.SendAsync(new GetCustomerLookup(), Ct));

    public async Task<IActionResult> OnGetDetailsAsync(int id)
    {
        var page = await Dispatcher.SendAsync(new GetOrderDetails(id), Ct);
        if (page is null)
        {
            return NotFound();
        }

        HttpContext.Session.SetInt32(LastViewedKey, id);
        page.CanUpload = CanManage;
        return Partial("_OrderDetails", page);
    }


    public async Task<IActionResult> OnGetDownloadAsync(int id, string name)
    {
        var file = await Dispatcher.SendAsync(new DownloadAttachment(id, name), Ct);
        return file is null ? NotFound() : File(file.Content, "application/octet-stream", file.FileName);
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, IEnumerable<IFormFile> files)
    {
        if (!CanManage) return Forbid();

        var streams = files.Select(f => new AttachmentUpload(f.FileName, f.Length, f.OpenReadStream())).ToList();
        try
        {
            var result = await Dispatcher.SendAsync(new UploadAttachments(id, streams), Ct);
            return result.Succeeded ? new JsonResult(new { }) : BadRequest(result.Errors[0].Message);
        }
        finally
        {
            foreach (var s in streams) await s.Content.DisposeAsync();
        }
    }

    public async Task<IActionResult> OnPostRemoveFileAsync(int id, string[] fileNames)
    {
        if (!CanManage) return Forbid();

        await Dispatcher.SendAsync(new RemoveAttachments(id, fileNames ?? Array.Empty<string>()), Ct);
        return new JsonResult(new { });
    }

    // [Authorize] is ignored on handler methods, so the permission is checked in each one.

    public async Task<IActionResult> OnPostCreateAsync([DataSourceRequest] DataSourceRequest request, OrderViewModel order)
    {
        if (!CanManage) return Forbid();

        if (ModelState.IsValid)
        {
            var result = await Dispatcher.SendAsync(new CreateOrder(order), Ct);
            if (result.Succeeded) order = result.Value; else AddErrors(result);
        }
        return new JsonResult(new[] { order }.ToDataSourceResult(request, ModelState));
    }

    public async Task<IActionResult> OnPostUpdateAsync([DataSourceRequest] DataSourceRequest request, OrderViewModel order)
    {
        if (!CanManage) return Forbid();

        if (ModelState.IsValid)
        {
            var result = await Dispatcher.SendAsync(new UpdateOrder(order), Ct);
            if (result.Succeeded) order = result.Value; else AddErrors(result);
        }
        return new JsonResult(new[] { order }.ToDataSourceResult(request, ModelState));
    }

    public async Task<IActionResult> OnPostDestroyAsync([DataSourceRequest] DataSourceRequest request, int orderId, byte[] rowVersion)
    {
        if (!CanManage) return Forbid();

        var result = await Dispatcher.SendAsync(new DeleteOrder(orderId, rowVersion), Ct);
        if (!result.Succeeded) AddErrors(result);

        return new JsonResult(new[] { new OrderViewModel { OrderId = orderId } }.ToDataSourceResult(request, ModelState));
    }

    private void AddErrors(Result result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }
    }
}
