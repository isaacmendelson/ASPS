using Business.Services;
using WebApi.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

// WebApi does NOT connect to database directly!
// All DB operations go through CQRS → ASPSBackend
// (removed AddBusinessServices - not needed here)

// Configure forwarded headers for reverse proxy (Tailscale)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                               ForwardedHeaders.XForwardedProto | 
                               ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add Razor Pages for Admin UI
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/", "AdminPolicy");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Logout");
    options.Conventions.AllowAnonymousToPage("/DeviceLogin");
    options.Conventions.AllowAnonymousToPage("/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/DebugClaims");
});

// Authorization policy - require Admin role (set from Keycloak Administrators group)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });
});

// ========================================
// KEYCLOAK AUTHENTICATION
// Optional: if Keycloak:ClientId is not configured (e.g. local dev without
// a Keycloak server), falls back to simple cookie-only auth (no SSO).
// ========================================
var keycloakSection = builder.Configuration.GetSection("Keycloak");
var keycloakEnabled = !string.IsNullOrWhiteSpace(keycloakSection["ClientId"]);

if (keycloakEnabled)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "ASPS.Auth";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = keycloakSection["Authority"];
        options.ClientId = keycloakSection["ClientId"];
        options.ClientSecret = keycloakSection["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.CorrelationCookie.SameSite = SameSiteMode.None;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "roles",
            ValidateIssuer = true
        };

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (context.Request.Headers.ContainsKey("X-Forwarded-Proto"))
                {
                    var scheme = context.Request.Headers["X-Forwarded-Proto"].ToString();
                    if (scheme == "https")
                        context.ProtocolMessage.RedirectUri = context.ProtocolMessage.RedirectUri?.Replace("http://", "https://");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var username = principal?.FindFirst("preferred_username")?.Value 
                            ?? principal?.Identity?.Name;
                
                // Check if user is admin
                // Option 1: Check known admin usernames (temporary until Keycloak groups are configured)
                var adminUsernames = new[] { "asps-admin", "isaac", "admin" };
                var isAdmin = adminUsernames.Contains(username?.ToLower());
                
                Console.WriteLine($"DEBUG: username={username}, checking against: {string.Join(",", adminUsernames)}");
                
                // Option 2: Check groups claim (if configured in Keycloak)
                if (!isAdmin)
                {
                    var groupsClaims = principal?.FindAll("groups");
                    if (groupsClaims != null)
                    {
                        foreach (var claim in groupsClaims)
                        {
                            if (claim.Value.Contains("Administrators") || claim.Value == "/Administrators")
                            {
                                isAdmin = true;
                                break;
                            }
                        }
                    }
                }
                
                // Option 3: Check realm_access roles
                var realmAccess = principal?.FindFirst("realm_access")?.Value;
                if (!isAdmin && realmAccess?.Contains("admin") == true)
                {
                    isAdmin = true;
                }
                
                // Add admin role if in Administrators group
                if (isAdmin && principal?.Identity is ClaimsIdentity identity)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
                }
                
                // Log all claims for debugging
                Console.WriteLine($"✓ User authenticated: {username}, Admin: {isAdmin}");
                if (principal != null)
                {
                    foreach (var claim in principal.Claims)
                    {
                        Console.WriteLine($"  Claim: {claim.Type} = {claim.Value}");
                    }
                }
                
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"✗ Auth failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    });

    Console.WriteLine("✓ Keycloak SSO authentication configured");
}
else
{
    // Dev fallback: cookie-only auth, no Keycloak required
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Cookie.HttpOnly = true;
            options.Cookie.Name = "ASPS.Auth";
        });

    Console.WriteLine("⚠ Keycloak not configured — running with cookie-only auth (dev mode)");
}

// ========================================
// CQRS CLIENT
// ========================================
builder.Services.AddSingleton<CurveKeyManager>();

var cqrsEndpoint = builder.Configuration.GetValue<string>("CQRS:Endpoint") ?? "tcp://localhost:5556";

builder.Services.AddSingleton<ICQRSClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CQRSClient>>();
    var curveKeyManager = sp.GetRequiredService<CurveKeyManager>();
    return new CQRSClient(cqrsEndpoint, logger, curveKeyManager);
});

Console.WriteLine($"✓ CQRS Client configured: {cqrsEndpoint}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ASPSBackend API", Version = "v1" });
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<NetMQClientService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<NetMQClientService>>();
    var curveKeyManager = sp.GetRequiredService<CurveKeyManager>();
    var endpoint = configuration.GetValue<string>("NetMQ:BusinessEndpoint") ?? "tcp://localhost:5555";
    return new NetMQClientService(endpoint, logger, curveKeyManager);
});

// SimulationRunner - background service for running simulations
builder.Services.AddSingleton<SimulationRunner>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationRunner>());

var app = builder.Build();

// IMPORTANT: UseForwardedHeaders MUST be first!
app.UseForwardedHeaders();

// HTTPS configuration
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<WebApi.Hubs.NotificationsHub>("/notificationshub");

var webApiVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.1";
Console.WriteLine("========================================");
Console.WriteLine($"[INFO] WebApi v{webApiVersion} starting...");
Console.WriteLine("ASPS Admin Dashboard");
Console.WriteLine($"Authentication: {(keycloakEnabled ? "Keycloak SSO ✓" : "Cookie-only (dev mode) ⚠")}");
Console.WriteLine("ForwardedHeaders: Enabled ✓");
Console.WriteLine("========================================");

app.Run();

// Make the Program class accessible for integration tests
public partial class Program { }
