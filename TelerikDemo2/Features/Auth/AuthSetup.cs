using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace TelerikDemo2.Features.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public sealed record DemoUser(string UserName, string Password, string Role);

public interface IUserStore
{
    DemoUser Validate(string userName, string password);
}

// Hardcoded demo accounts (see README).
public sealed class DemoUserStore : IUserStore
{
    private static readonly DemoUser[] Users =
    {
        new("admin", "Admin123!", Roles.Admin),
        new("user", "User123!", Roles.User)
    };

    public DemoUser Validate(string userName, string password) =>
        Users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase) && u.Password == password);
}

public static class AuthSetup
{
    public static IServiceCollection AddDemoAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IUserStore, DemoUserStore>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                // AJAX and API calls get 401/403 instead of a redirect to the login page.
                options.Events.OnRedirectToLogin = ctx => RedirectOrStatus(ctx, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = ctx => RedirectOrStatus(ctx, StatusCodes.Status403Forbidden);
            });

        services.AddAuthorization(options =>
        {
            // Sign-in required everywhere unless a page has [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        return services;
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> ctx, int status)
    {
        var isAjaxOrApi = ctx.Request.Headers.XRequestedWith == "XMLHttpRequest"
                          || ctx.Request.Path.StartsWithSegments("/api");
        if (isAjaxOrApi)
        {
            ctx.Response.StatusCode = status;
        }
        else
        {
            ctx.Response.Redirect(ctx.RedirectUri);
        }
        return Task.CompletedTask;
    }
}
