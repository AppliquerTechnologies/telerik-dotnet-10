using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelerikDemo2.Features;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Orders/Index");
}
