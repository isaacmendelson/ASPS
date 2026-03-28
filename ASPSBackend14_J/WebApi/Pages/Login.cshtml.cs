using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace WebApi.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; set; }

    public bool KeycloakEnabled { get; private set; }

    public LoginModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        KeycloakEnabled = !string.IsNullOrWhiteSpace(_configuration["Keycloak:ClientId"]);
    }

    public IActionResult OnGetLogin()
    {
        var keycloakEnabled = !string.IsNullOrWhiteSpace(_configuration["Keycloak:ClientId"]);

        if (keycloakEnabled)
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = "/"
            }, OpenIdConnectDefaults.AuthenticationScheme);
        }

        // Dev mode - show login form
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var keycloakEnabled = !string.IsNullOrWhiteSpace(_configuration["Keycloak:ClientId"]);

        if (keycloakEnabled)
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = "/"
            }, OpenIdConnectDefaults.AuthenticationScheme);
        }

        // Dev mode - simple auth (any user/pass works)
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required";
            return Page();
        }

        // Create claims for dev user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, Username),
            new Claim(ClaimTypes.Email, $"{Username}@dev.local"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("preferred_username", Username),
            new Claim("sub", Username)  // Use username as sub for dev mode
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToPage("/Index");
    }
}
