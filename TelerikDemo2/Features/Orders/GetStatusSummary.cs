using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

public sealed record GetStatusSummary : IRequest<IReadOnlyList<StatusSummaryViewModel>>;

public class StatusSummaryViewModel
{
    public OrderStatus Status { get; set; }
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
}

public sealed class GetStatusSummaryHandler(AppDbContext db)
    : IRequestHandler<GetStatusSummary, IReadOnlyList<StatusSummaryViewModel>>
{
    public async Task<IReadOnlyList<StatusSummaryViewModel>> HandleAsync(GetStatusSummary request, CancellationToken ct) =>
        await db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new StatusSummaryViewModel { Status = g.Key, Count = g.Count(), TotalValue = g.Sum(o => o.Total) })
            .OrderBy(x => x.Status)
            .ToListAsync(ct);
}
