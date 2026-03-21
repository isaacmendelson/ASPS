using Business.Data.EF;
using Common.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ASPS.Tests.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Sets up an in-memory database and configures test services.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing AppDbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<AppDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<TestWebApplicationFactory>>();

                // Ensure the database is created
                db.Database.EnsureCreated();

                try
                {
                    // Seed the database with test data if needed
                    SeedDatabase(db);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred seeding the database with test data.");
                }
            }
        });
    }

    /// <summary>
    /// Seed the database with test data
    /// </summary>
    private static void SeedDatabase(AppDbContext context)
    {
        // Clear existing data
        context.TrackedDomains.RemoveRange(context.TrackedDomains);
        context.SaveChanges();

        // Add test tracked domains
        context.TrackedDomains.AddRange(
            new TrackedDomain("google-analytics.com", "Analytics"),
            new TrackedDomain("doubleclick.net", "Advertising"),
            new TrackedDomain("facebook.com", "Social"),
            new TrackedDomain("ads.google.com", "Advertising")
        );

        context.SaveChanges();
    }

    /// <summary>
    /// Get a scoped database context for test operations
    /// </summary>
    public AppDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
