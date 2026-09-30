using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;
using TelerikDemo2.Infrastructure.Storage;

namespace TelerikDemo2.Features.Orders;

// Null when the order does not exist.
public sealed record GetOrderDetails(int OrderId) : IRequest<OrderDetailsPageViewModel>;

public class OrderDetailsViewModel
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerCity { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public string Notes { get; set; }
}

public sealed record AttachmentViewModel(string FileName, long SizeBytes);

public class OrderDetailsPageViewModel
{
    public OrderDetailsViewModel Details { get; set; }
    public IReadOnlyList<AttachmentViewModel> Attachments { get; set; } = Array.Empty<AttachmentViewModel>();
    public bool CanUpload { get; set; }

    // Same limits the server enforces, so the Upload widget can validate before sending.
    public long MaxUploadBytes { get; set; }
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}

public sealed class GetOrderDetailsHandler(AppDbContext db, IAttachmentStore attachments, IOptions<AttachmentOptions> options)
    : IRequestHandler<GetOrderDetails, OrderDetailsPageViewModel>
{
    public async Task<OrderDetailsPageViewModel> HandleAsync(GetOrderDetails request, CancellationToken ct)
    {
        var details = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.OrderId)
            .Select(o => new OrderDetailsViewModel
            {
                OrderId = o.Id,
                CustomerName = o.Customer.Name,
                CustomerEmail = o.Customer.Email,
                CustomerCity = o.Customer.City,
                OrderDate = o.OrderDate,
                Total = o.Total,
                Status = o.Status,
                Notes = o.Notes
            })
            .FirstOrDefaultAsync(ct);

        if (details is null)
        {
            return null;
        }

        return new OrderDetailsPageViewModel
        {
            Details = details,
            Attachments = attachments.List(request.OrderId)
                .Select(f => new AttachmentViewModel(f.Name, f.SizeBytes))
                .ToList(),
            MaxUploadBytes = options.Value.MaxBytes,
            AllowedExtensions = options.Value.AllowedExtensions
        };
    }
}
