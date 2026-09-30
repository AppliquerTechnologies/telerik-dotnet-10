namespace TelerikDemo2.Domain;

public sealed record RuleViolation(string Field, string Message);

// Order business rules. No database or UI here: handlers load the facts and pass them in.
// The limits are constants so the view model, editor templates, EF mapping and seeder share them.
public static class OrderRules
{
    public const int MinTotal = 10;
    public const int MaxTotal = 5000;
    public const int NotesMaxLength = 500;
    public const OrderStatus InitialStatus = OrderStatus.New;

    public const string NotFoundMessage = "The order no longer exists.";


    public static bool IsLocked(OrderStatus status) => status == OrderStatus.Shipped;

    public static DateTime LatestOrderDate(DateTime today) => today.Date;

    public static RuleViolation CanCreateAs(OrderStatus status) =>
        IsLocked(status)
            ? new RuleViolation(nameof(Order.Status), "A new order cannot start as Shipped.")
            : null;

    public static RuleViolation CanModify(Order order) =>
        IsLocked(order.Status)
            ? new RuleViolation(nameof(Order.Status), "Shipped orders cannot be changed.")
            : null;

    public static RuleViolation CanDelete(Order order) =>
        IsLocked(order.Status)
            ? new RuleViolation(nameof(Order.Status), "Shipped orders cannot be deleted.")
            : null;

    public static RuleViolation CheckTotal(decimal? total) =>
        total is null || total < MinTotal || total > MaxTotal
            ? new RuleViolation(nameof(Order.Total), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Total must be between {MinTotal:N0} and {MaxTotal:N0}."))
            : null;

    // Shared by create and update.
    public static IEnumerable<RuleViolation> CheckDetails(bool customerExists, DateTime? orderDate, decimal? total, DateTime today)
    {
        if (!customerExists)
        {
            yield return new RuleViolation(nameof(Order.CustomerId), "The selected customer does not exist.");
        }
        if (orderDate is null || orderDate.Value.Date > LatestOrderDate(today))
        {
            yield return new RuleViolation(nameof(Order.OrderDate), "Order date cannot be in the future.");
        }
        if (CheckTotal(total) is { } totalViolation)
        {
            yield return totalViolation;
        }
    }
}
