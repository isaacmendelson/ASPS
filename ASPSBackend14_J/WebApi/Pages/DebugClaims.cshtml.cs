using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApi.Pages;

[AllowAnonymous]  // Allow access without admin role for debugging
public class DebugClaimsModel : PageModel
{
    public void OnGet()
    {
    }
}
