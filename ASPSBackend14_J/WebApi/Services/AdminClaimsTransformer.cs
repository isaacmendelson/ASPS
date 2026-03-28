using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace WebApi.Services;

public class AdminClaimsTransformer : IClaimsTransformation
{
    private static readonly string[] AdminUsernames = { "asps-admin", "isaac", "admin" };
    
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Skip if already has Admin role
        if (principal.IsInRole("Admin"))
        {
            return Task.FromResult(principal);
        }
        
        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }
        
        var username = principal.FindFirst("preferred_username")?.Value 
                    ?? principal.Identity?.Name;
        
        var isAdmin = false;
        
        // Check known admin usernames
        if (AdminUsernames.Contains(username?.ToLower()))
        {
            isAdmin = true;
        }
        
        // Check groups claim
        if (!isAdmin)
        {
            var groupsClaims = principal.FindAll("groups");
            foreach (var claim in groupsClaims)
            {
                if (claim.Value.Contains("Administrators") || claim.Value == "Administrators")
                {
                    isAdmin = true;
                    break;
                }
            }
        }
        
        if (isAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        }
        
        return Task.FromResult(principal);
    }
}
