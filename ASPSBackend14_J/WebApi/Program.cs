using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

// Add Razor Pages for Admin UI
builder.Services.AddRazorPages();

// ========================================
// CQRS CLIENT - Sends Commands/Queries to Business layer
// ========================================
// WebApi has ZERO database access!
// All data operations go through NetMQ to ASPSBackend process

var cqrsEndpoint = builder.Configuration.GetValue<string>("CQRS:Endpoint") ?? "tcp://localhost:5556";

builder.Services.AddSingleton<CQRSClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CQRSClient>>();
    return new CQRSClient(cqrsEndpoint, logger);
});

Console.WriteLine($"✓ CQRS Client configured: {cqrsEndpoint}");
Console.WriteLine("✓ WebApi has NO database access - all operations via NetMQ");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "ASPSBackend API", Version = "v1" });
});

// Add SignalR
builder.Services.AddSignalR();

// Add NetMQ Client Service (for alerts)
builder.Services.AddSingleton<NetMQClientService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<NetMQClientService>>();
    var endpoint = configuration.GetValue<string>("NetMQ:BusinessEndpoint") ?? "tcp://localhost:5555";
    return new NetMQClientService(endpoint, logger);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Map endpoints
app.MapControllers();
app.MapRazorPages();

// Map SignalR Hub
app.MapHub<WebApi.Hubs.NotificationsHub>("/notificationshub");

Console.WriteLine("========================================");
Console.WriteLine("WebApi started - Distributed CQRS Architecture");
Console.WriteLine("Admin Dashboard: http://localhost:5001");
Console.WriteLine("Swagger UI: https://localhost:7001/swagger");
Console.WriteLine("========================================");
Console.WriteLine("Architecture (Separate Processes):");
Console.WriteLine("  WebApi (Presentation)");
Console.WriteLine("    ↓ NetMQ (Commands/Queries)");
Console.WriteLine("  ASPSBackend (Business + Data)");
Console.WriteLine("    ↓ Repositories");
Console.WriteLine("  Database");
Console.WriteLine("========================================");
Console.WriteLine("WebApi: NO database access ✓");
Console.WriteLine("WebApi: NO repositories ✓");
Console.WriteLine("WebApi: ONLY NetMQ messaging ✓");
Console.WriteLine("========================================");

app.Run();
