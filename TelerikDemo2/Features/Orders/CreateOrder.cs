using Microsoft.EntityFrameworkCore;
using TelerikDemo2.Common;
using TelerikDemo2.Domain;
using TelerikDemo2.Infrastructure.Persistence;

namespace TelerikDemo2.Features.Orders;

public sealed record CreateOrder(OrderViewModel Model) : IRequest<Result<OrderViewModel>>;

public sealed class CreateOrderHandler(AppDbContext db, OrderInputValidator validator)
    : IRequestHandler<CreateOrder, Result<OrderViewModel>>
{
    public async Task<Result<OrderViewModel>> HandleAsync(CreateOrder request, CancellationToken ct)
    {
        var model = request.Model;
        var result = new Result<OrderViewModel>();

        result.AddViolation(OrderRules.CanCreateAs(model.Status));
        await validator.ValidateAsync(model, result, ct);
        if (!result.Succeeded)
        {
            return result;
        }

        var entity = new Order
        {
            CustomerId = model.CustomerId.Value,
            OrderDate = model.OrderDate.Value.Date,
            Total = model.Total.Value,
            Status = model.Status,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
        };
        db.Orders.Add(entity);
        await db.SaveChangesAsync(ct);

        var created = await db.Orders.AsNoTracking().Where(o => o.Id == entity.Id).ToViewModel().SingleAsync(ct);
        return Result<OrderViewModel>.Ok(created);
    }
}
