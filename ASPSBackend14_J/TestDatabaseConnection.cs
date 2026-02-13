using Business.Data.EF;
using Business.Data.EF.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TestDatabaseConnection;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("DATABASE CONNECTION TEST");
        Console.WriteLine("========================================");
        
        // Connection string - UPDATE THIS WITH YOUR PASSWORD
        var connectionString = "server=localhost;port=3306;database=ASPSBackend2DB;user=root;password=your_password;";
        
        Console.WriteLine($"Connection String: {connectionString.Replace("password=your_password", "password=***")}");
        Console.WriteLine();
        
        try
        {
            // Create DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString));
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            
            using (var context = new AppDbContext(optionsBuilder.Options))
            {
                Console.WriteLine("✓ DbContext created");
                
                // Test connection
                var canConnect = await context.Database.CanConnectAsync();
                Console.WriteLine($"✓ Can connect to database: {canConnect}");
                
                if (!canConnect)
                {
                    Console.WriteLine("❌ Cannot connect to database!");
                    Console.WriteLine("   Check your connection string and MySQL server");
                    return;
                }
                
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("LOADING USERS");
                Console.WriteLine("========================================");
                
                // Create UserRepository
                var userRepo = new UserRepository(context);
                var users = await userRepo.GetAllAsync();
                var userList = users.ToList();
                
                Console.WriteLine($"Users loaded: {userList.Count}");
                foreach (var user in userList)
                {
                    Console.WriteLine($"  - {user.FirstName} {user.LastName}");
                    Console.WriteLine($"    Key: {user.Key}");
                    Console.WriteLine($"    KeycloakUserId: {user.KeycloakUserId}");
                    Console.WriteLine($"    IsDeleted: {user.IsDeleted}");
                    Console.WriteLine($"    IsDisabled: {user.IsDisabled}");
                }
                
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("LOADING USER DEVICES");
                Console.WriteLine("========================================");
                
                // Create UserDeviceRepository
                var deviceRepo = new UserDeviceRepository(context);
                var devices = await deviceRepo.GetAllAsync();
                var deviceList = devices.ToList();
                
                Console.WriteLine($"Devices loaded: {deviceList.Count}");
                foreach (var device in deviceList)
                {
                    Console.WriteLine($"  - {device.DeviceUid}");
                    Console.WriteLine($"    Key: {device.Key}");
                    Console.WriteLine($"    UserKey: {device.UserKey}");
                    Console.WriteLine($"    Type: {device.GetType().Name}");
                    Console.WriteLine($"    IsDeleted: {device.IsDeleted}");
                }
                
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("TEST COMPLETE");
                Console.WriteLine("========================================");
                
                if (userList.Count == 0)
                {
                    Console.WriteLine("❌ NO USERS FOUND!");
                    Console.WriteLine("   Run: mysql -u root -p < create-database.sql");
                }
                else if (deviceList.Count == 0)
                {
                    Console.WriteLine("❌ NO DEVICES FOUND!");
                    Console.WriteLine("   Check discriminator values in database");
                    Console.WriteLine("   Run: mysql -u root -p ASPSBackend2DB < DEBUG-FULL-DATABASE.sql");
                }
                else
                {
                    Console.WriteLine("✓ SUCCESS - Data loaded correctly!");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ ERROR:");
            Console.WriteLine($"   {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                Console.WriteLine();
                Console.WriteLine("Inner exception:");
                Console.WriteLine(ex.InnerException.Message);
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
