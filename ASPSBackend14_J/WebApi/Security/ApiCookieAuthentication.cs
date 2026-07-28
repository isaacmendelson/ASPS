using Microsoft.AspNetCore.Authentication.Cookies;

namespace WebApi.Security;

public static class ApiCookieAuthentication
{
    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Events.OnRedirectToLogin = context =>
        {
            if (IsApiOrHubRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (IsApiOrHubRequest(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    }

    public static bool IsApiOrHubRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api") ||
               request.Path.StartsWithSegments("/notificationshub");
    }
}
