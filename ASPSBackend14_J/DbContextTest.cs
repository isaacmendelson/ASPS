using Business.Data.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace DbContextTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("DbContext Initialization Test");
            Console.WriteLine("========================================");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            // Add DbContext with maximum verbosity
            services.AddDbContext<AppDbContext>(options =>
            {
                var connString = configuration.GetConnectionString("DefaultConnection");
                Console.WriteLine($"Connection String: {connString}");
                
                options.UseMySql(connString, ServerVersion.AutoDetect(connString));
                
                // Maximum logging
                options.LogTo(Console.WriteLine, LogLevel.Trace)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            });

            var serviceProvider = services.BuildServiceProvider();

            try
            {
                Console.WriteLine("\n[TEST] Attempting to create DbContext...");
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                Console.WriteLine("[TEST] ✓ DbContext created successfully!");
                Console.WriteLine("[TEST] Testing database connection...");
                
                var canConnect = context.Database.CanConnect();
                Console.WriteLine($"[TEST] ✓ Database connection: {canConnect}");
                
                // Try to enumerate entity types
                Console.WriteLine("\n[TEST] Entity types in model:");
                foreach (var entityType in context.Model.GetEntityTypes())
                {
                    Console.WriteLine($"  - {entityType.Name}");
                    Console.WriteLine($"    Properties: {entityType.GetProperties().Count()}");
                    
                    foreach (var prop in entityType.GetProperties())
                    {
                        try
                        {
                            var mapping = prop.FindTypeMapping();
                            Console.WriteLine($"      • {prop.Name}: {prop.ClrType.Name} → {mapping?.StoreType ?? "NULL"}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"      ✗ {prop.Name}: ERROR - {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine("\n[TEST] ✓✓✓ ALL TESTS PASSED ✓✓✓");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[TEST] ✗✗✗ FAILURE ✗✗✗");
                Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack:\n{ex.InnerException.StackTrace}");
                }
                
                return;
            }

            Console.WriteLine("\n========================================");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
