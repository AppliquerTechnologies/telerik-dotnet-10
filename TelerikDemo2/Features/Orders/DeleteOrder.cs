using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

public sealed record DeleteOrder(int OrderId) : IRequest<Result>;

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

        db.Orders.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
