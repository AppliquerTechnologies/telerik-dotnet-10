using System.Security.Claims;

namespace TelerikDemo2.Features.Auth;

// Who may do what. The handlers enforce it and the views use it to hide buttons.
public static class Permissions
{
    public static bool CanManageOrders(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
