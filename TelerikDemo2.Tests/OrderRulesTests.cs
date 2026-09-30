using TelerikDemo2.Domain;
using Xunit;

namespace TelerikDemo2.Tests;

public class OrderRulesTests
{
    private static readonly DateTime Today = new(2026, 9, 30);

    [Fact]
    public void New_order_cannot_start_as_shipped()
    {
        var v = OrderRules.CanCreateAs(OrderStatus.Shipped);
        Assert.Equal(nameof(Order.Status), v.Field);
        Assert.Null(OrderRules.CanCreateAs(OrderStatus.New));
        Assert.Null(OrderRules.CanCreateAs(OrderStatus.Processing));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.New, false)]
    [InlineData(OrderStatus.Processing, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Only_shipped_orders_are_locked(OrderStatus status, bool locked)
    {
        var order = new Order { Status = status };
        Assert.Equal(locked, OrderRules.CanModify(order) is not null);
        Assert.Equal(locked, OrderRules.CanDelete(order) is not null);
    }

    [Theory]
    [InlineData(9.99, false)]
    [InlineData(10, true)]
    [InlineData(5000, true)]
    [InlineData(5000.01, false)]
    public void Total_must_be_within_limits(double total, bool valid) =>
        Assert.Equal(valid, OrderRules.CheckTotal((decimal)total) is null);

    [Fact]
    public void Future_date_is_rejected_but_today_is_allowed()
    {
        Assert.Empty(OrderRules.CheckDetails(true, Today, 100, Today));
        var errors = OrderRules.CheckDetails(true, Today.AddDays(1), 100, Today).ToList();
        Assert.Single(errors);
        Assert.Equal(nameof(Order.OrderDate), errors[0].Field);
    }

    [Fact]
    public void Unknown_customer_is_reported_on_the_customer_field()
    {
        var errors = OrderRules.CheckDetails(false, Today, 100, Today).ToList();
        Assert.Equal(nameof(Order.CustomerId), Assert.Single(errors).Field);
    }
}
