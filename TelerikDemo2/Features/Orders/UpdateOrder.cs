using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

public sealed record UpdateOrder(OrderViewModel Model) : IRequest<Result<OrderViewModel>>;

public sealed class UpdateOrderHandler(AppDbContext db, OrderInputValidator validator)
    : IRequestHandler<UpdateOrder, Result<OrderViewModel>>
{
    public async Task<Result<OrderViewModel>> HandleAsync(UpdateOrder request, CancellationToken ct)
    {
        var model = request.Model;
        var entity = await db.Orders.FirstOrDefaultAsync(o => o.Id == model.OrderId, ct);
        if (entity is null)
        {
            return Result<OrderViewModel>.Fail(string.Empty, OrderRules.NotFoundMessage);
        }

        var result = new Result<OrderViewModel>();
        if (OrderRules.CanModify(entity) is { } locked)
        {
            return (Result<OrderViewModel>)result.AddViolation(locked);
        }
        await validator.ValidateAsync(model, result, ct);
        if (!result.Succeeded)
        {
            return result;
        }

        entity.CustomerId = model.CustomerId.Value;
        entity.OrderDate = model.OrderDate.Value.Date;
        entity.Total = model.Total.Value;
        entity.Status = model.Status;
        entity.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        await db.SaveChangesAsync(ct);

        var updated = await db.Orders.AsNoTracking().Where(o => o.Id == entity.Id).ToViewModel().SingleAsync(ct);
        return Result<OrderViewModel>.Ok(updated);
    }
}
