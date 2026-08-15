using Business.Services;
using Business.Messaging;
using WebApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using WebApi.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.ContractResolver =
            new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter());
        options.SerializerSettings.DateTimeZoneHandling =
            Newtonsoft.Json.DateTimeZoneHandling.Utc;
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

// ========================================
// CORS — allow Angular admin SPA
// Origins are configurable via Cors:AllowedOrigins in appsettings.
// ========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularAdmin", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:4200" };

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
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
    var adminPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("Admin")
        .Build();

    options.AddPolicy("AdminPolicy", adminPolicy);
    options.AddPolicy("NotificationsHubPolicy", policy =>
        policy.AddRequirements(new NotificationsHubRequirement()));
    options.FallbackPolicy = adminPolicy;
});

// Claims transformer to add Admin role based on username/groups.
// IClaimsTransformation is invoked by ASP.NET Core after every successful
// authentication, regardless of scheme — applies to both Cookie and JWT principals.
builder.Services.AddScoped<IClaimsTransformation, AdminClaimsTransformer>();

// ========================================
// KEYCLOAK AUTHENTICATION
// Optional: if Keycloak:ClientId is not configured (e.g. local dev without
// a Keycloak server), falls back to simple cookie-only auth (no SSO).
// ========================================
var keycloakSection = builder.Configuration.GetSection("Keycloak");
var keycloakEnabled = !string.IsNullOrWhiteSpace(keycloakSection["ClientId"]);

if (keycloakEnabled)
{
    // Dual-scheme: Cookie for Razor pages, JWT Bearer for Angular SPA.
    // A PolicyScheme inspects the request and forwards to the correct handler.
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "SmartScheme";
        options.DefaultChallengeScheme = "SmartScheme";
    })
    .AddPolicyScheme("SmartScheme", "Cookie or JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // If the request carries a Bearer token, use JWT handler
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            // SignalR WebSocket upgrades carry the token as a query parameter
            if (context.Request.Path.StartsWithSegments("/notificationshub") &&
                context.Request.Query.ContainsKey("access_token"))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            // Default: Cookie auth (Razor pages)
            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
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
        ApiCookieAuthentication.Configure(options);
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
            RoleClaimType = ClaimTypes.Role,  // Use standard .NET role claim type
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
    })
    .AddJwtBearer(options =>
    {
        // Validates JWT tokens issued by Keycloak for the asps-angular-admin client (PKCE/public)
        options.Authority = keycloakSection["Authority"];
        options.Audience = "asps-angular-admin";
        options.RequireHttpsMetadata = false; // dev only — set to true in production via appsettings.Production.json

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudiences = new[] { "asps-angular-admin", "account" },
        };

        // SignalR sends the access token as a query string parameter during the
        // WebSocket upgrade handshake (HTTP headers are not available at that point).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/notificationshub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    Console.WriteLine("✓ Keycloak SSO authentication configured (Cookie + JWT Bearer dual scheme)");
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
            ApiCookieAuthentication.Configure(options);
        });

    Console.WriteLine("⚠ Keycloak not configured — running with cookie-only auth (dev mode)");
}

// ========================================
// CQRS CLIENT
// ========================================
builder.Services.AddSingleton<CurveKeyManager>();
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new CqrsChannelSecurity(new CqrsChannelSecurityOptions
    {
        ClientId = configuration["CQRS:ClientId"] ?? "asps-webapi",
        SharedSecret = configuration["CQRS:SharedSecret"] ?? string.Empty
    });
});

var cqrsEndpoint = builder.Configuration.GetValue<string>("CQRS:Endpoint") ?? "tcp://localhost:5556";

// ASPS-687 (Messaging Refactoring Phase 4): NetMQCqrsClient (Business.Messaging.Transport.NetMQ)
// replaces CQRSClient as the canonical transport implementation. It implements the
// transport-agnostic Business.Messaging.Abstractions.ICqrsClient directly; the legacy
// ICQRSClient contract is bridged by NetMQCqrsClientAdapter (Business cannot reference WebApi,
// so the adapter — not the transport class — implements ICQRSClient). Both resolve to the
// same underlying singleton, so all 39 existing WebApi consumers keep working unmodified.
builder.Services.AddSingleton<Business.Messaging.Transport.NetMQ.NetMQCqrsClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Business.Messaging.Transport.NetMQ.NetMQCqrsClient>>();
    var curveKeyManager = sp.GetRequiredService<CurveKeyManager>();
    var channelSecurity = sp.GetRequiredService<CqrsChannelSecurity>();
    return new Business.Messaging.Transport.NetMQ.NetMQCqrsClient(cqrsEndpoint, logger, curveKeyManager, channelSecurity);
});
builder.Services.AddSingleton<Business.Messaging.Abstractions.ICqrsClient>(
    sp => sp.GetRequiredService<Business.Messaging.Transport.NetMQ.NetMQCqrsClient>());
builder.Services.AddSingleton<ICQRSClient>(
    sp => new NetMQCqrsClientAdapter(sp.GetRequiredService<Business.Messaging.Abstractions.ICqrsClient>()));
builder.Services.AddSingleton<IAuthorizationHandler, NotificationsHubAuthorizationHandler>();
builder.Services.AddSingleton<
    IAuthorizationMiddlewareResultHandler,
    ApiAuthorizationMiddlewareResultHandler>();

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

// CORS must come after UseRouting and before UseAuthentication/UseAuthorization
app.UseCors("AngularAdmin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
app.MapHub<WebApi.Hubs.NotificationsHub>("/notificationshub")
    .RequireAuthorization("NotificationsHubPolicy");

var webApiVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0.1";
Console.WriteLine("========================================");
Console.WriteLine($"[INFO] WebApi v{webApiVersion} starting...");
Console.WriteLine("ASPS Admin Dashboard");
Console.WriteLine($"Authentication: {(keycloakEnabled ? "Keycloak SSO + JWT Bearer ✓" : "Cookie-only (dev mode) ⚠")}");
Console.WriteLine("ForwardedHeaders: Enabled ✓");
Console.WriteLine("CORS: AngularAdmin policy enabled ✓");
Console.WriteLine("========================================");

app.Run();

// Make the Program class accessible for integration tests
public partial class Program { }
