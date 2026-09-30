using System.ComponentModel.DataAnnotations;
using TelerikDemo2.Domain;

namespace TelerikDemo2.Features.Orders;

public class OrderViewModel
{
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Please select a customer.")]
    [Display(Name = "Customer")]
    [UIHint("CustomerDropDown")]
    public int? CustomerId { get; set; }

    // Filled by the query projection; not editable.
    [Display(Name = "Customer")]
    public string CustomerName { get; set; }

    [Required(ErrorMessage = "Order date is required.")]
    [Display(Name = "Order Date")]
    [DataType(DataType.Date)]
    [UIHint("OrderDatePicker")]
    public DateTime? OrderDate { get; set; }

    [Required(ErrorMessage = "Total is required.")]
    [OrderTotal]
    public decimal? Total { get; set; }

    public OrderStatus Status { get; set; } = OrderRules.InitialStatus;

    [StringLength(OrderRules.NotesMaxLength, ErrorMessage = "Notes cannot exceed {1} characters.")]
    public string Notes { get; set; }
}

// Applies OrderRules.CheckTotal as a DataAnnotation.
public sealed class OrderTotalAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext context) =>
        value is null || OrderRules.CheckTotal(value as decimal?) is not { } violation
            ? ValidationResult.Success
            : new ValidationResult(violation.Message);
}

public class StatusItem
{
    public int Value { get; set; }
    public string Text { get; set; }

    public static List<StatusItem> All() =>
        Enum.GetValues<OrderStatus>().Select(s => new StatusItem { Value = (int)s, Text = s.ToString() }).ToList();
}
