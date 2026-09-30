using TelerikDemo2.Domain;

namespace TelerikDemo2.Features.Orders;

public static class OrderProjection
{
    public static IQueryable<OrderViewModel> ToViewModel(this IQueryable<Order> orders) =>
        orders.Select(o => new OrderViewModel
        {
            OrderId = o.Id,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer.Name,
            OrderDate = o.OrderDate,
            Total = o.Total,
            Status = o.Status,
            Notes = o.Notes
        });
}
