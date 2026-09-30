namespace TelerikDemo2.Domain;

public enum OrderStatus
{
    New = 0,
    Processing = 1,
    Shipped = 2,
    Cancelled = 3
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string City { get; set; }
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public string Notes { get; set; }
}
