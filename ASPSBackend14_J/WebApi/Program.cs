using Business.Services;
using WebApi.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

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
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
});

// ========================================
// KEYCLOAK AUTHENTICATION
// ========================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "ASPS.Auth";
})
.AddOpenIdConnect(options =>
{
    var keycloakConfig = builder.Configuration.GetSection("Keycloak");
    
    options.Authority = keycloakConfig["Authority"];
    options.ClientId = keycloakConfig["ClientId"];
    options.ClientSecret = keycloakConfig["ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.RequireHttpsMetadata = false;
    
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.NonceCookie.SameSite = SameSiteMode.Unspecified;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;
    
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
            // Force HTTPS for redirect URI when behind proxy
            if (context.Request.Headers.ContainsKey("X-Forwarded-Proto"))
            {
                var scheme = context.Request.Headers["X-Forwarded-Proto"].ToString();
                if (scheme == "https")
                {
                    context.ProtocolMessage.RedirectUri = context.ProtocolMessage.RedirectUri?.Replace("http://", "https://");
                }
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var username = context.Principal?.Identity?.Name;
            Console.WriteLine($"✓ User authenticated: {username}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"✗ Auth failed: {context.Exception.Message}");
            return Task.CompletedTask;
        }
    };
});

Console.WriteLine("✓ Keycloak authentication configured");

// ========================================
// CQRS CLIENT
// ========================================
builder.Services.AddSingleton<CurveKeyManager>();

var cqrsEndpoint = builder.Configuration.GetValue<string>("CQRS:Endpoint") ?? "tcp://localhost:5556";

builder.Services.AddSingleton<CQRSClient>(sp =>
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

var app = builder.Build();

// IMPORTANT: UseForwardedHeaders MUST be first!
app.UseForwardedHeaders();

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

Console.WriteLine("========================================");
Console.WriteLine("ASPS Admin Dashboard");
Console.WriteLine("Authentication: Keycloak SSO ✓");
Console.WriteLine("ForwardedHeaders: Enabled ✓");
Console.WriteLine("========================================");

app.Run();
