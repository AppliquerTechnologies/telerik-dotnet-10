using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

public sealed record DeleteOrder(int OrderId, byte[] RowVersion) : IRequest<Result>;

public sealed class DeleteOrderHandler(AppDbContext db) : IRequestHandler<DeleteOrder, Result>
{
    public async Task<Result> HandleAsync(DeleteOrder request, CancellationToken ct)
    {
        var entity = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (entity is null)
        {
            return Result.Fail(string.Empty, OrderRules.NotFoundMessage);
        }
        if (OrderRules.CanDelete(entity) is { } locked)
        {
            return new Result().AddViolation(locked);
        }

        if (request.RowVersion is null)
        {
            return Result.Fail(nameof(OrderViewModel.RowVersion), OrderRules.ConflictMessage);
        }
        db.Entry(entity).Property(o => o.RowVersion).OriginalValue = request.RowVersion;

        db.Orders.Remove(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(nameof(OrderViewModel.RowVersion), OrderRules.ConflictMessage);
        }
        return Result.Ok();
    }
}
