using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelerikDemo2.Features.Auth;

[AllowAnonymous]
public class AccessDeniedModel : PageModel
{
    public void OnGet() { }
}
