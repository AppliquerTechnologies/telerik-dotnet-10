using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelerikDemo2.Features.Auth;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IUserStore _users;

    public LoginModel(IUserStore users) => _users = users;

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = _users.Validate(Input.UserName, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return Page();
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.Role, user.Role) },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/Orders");
    }

    public class LoginInput
    {
        [Required, Display(Name = "User name")]
        public string UserName { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
