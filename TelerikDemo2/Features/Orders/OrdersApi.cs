using TelerikDemo2.Common;

namespace TelerikDemo2.Features.Orders;

public static class OrdersApi
{
    public static IEndpointRouteBuilder MapOrdersApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/orders").RequireAuthorization();

        api.MapGet("/summary", async (IDispatcher dispatcher, CancellationToken ct) =>
            Results.Ok(await dispatcher.SendAsync(new GetStatusSummary(), ct)));

        api.MapGet("/{id:int}", async (int id, IDispatcher dispatcher, CancellationToken ct) =>
            await dispatcher.SendAsync(new GetOrderDetails(id), ct) is { } page
                ? Results.Ok(page.Details)
                : Results.NotFound());

        return app;
    }
}
