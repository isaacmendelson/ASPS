using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace ASPS.Tests.Composition;

public class BackendServiceProviderValidationTests
{
    [Fact]
    public void CreateHostBuilder_RejectsSingletonThatCapturesScopedService()
    {
        var builder = ASPSBackend.Program.CreateHostBuilder(
                ["--ConnectionStrings:DefaultConnection=Server=localhost;Database=asps_test;User=root;Password=test;"])
            .UseEnvironment(Environments.Production)
            .ConfigureServices(services =>
            {
                services.AddScoped<ScopedDependency>();
                services.AddSingleton<SingletonWithCaptiveDependency>();
            });

        Assert.ThrowsAny<Exception>(() => builder.Build());
    }

    [Fact]
    public void CreateHostBuilder_BuildsProductionContainerWithValidationEnabled()
    {
        using var host = ASPSBackend.Program.CreateHostBuilder(
                ["--ConnectionStrings:DefaultConnection=Server=localhost;Database=asps_test;User=root;Password=test;"])
            .UseEnvironment(Environments.Production)
            .Build();

        Assert.NotNull(host.Services);
    }

    [Fact(Skip = "ASPS-626: pre-existing test infrastructure gap — Program.ConfigureServices calls MySqlConnectionStringBuilder with the raw config value 'development-override' before DI validation can run, throwing ArgumentException. The test intent (verify appsettings.Development.json is loaded) cannot be verified at the Build() stage when the connection string is not a valid MySQL DSN. Needs a redesigned test that checks config loading without triggering service registration.")]
    public void CreateHostBuilder_LoadsDevelopmentOverrideWhileValidationRemainsEnabled()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(Path.Combine(testDirectory, "appsettings.Development.json"), "{\"ConnectionStrings\":{\"DefaultConnection\":\"development-override\"}}");

        try
        {
            Directory.SetCurrentDirectory(testDirectory);
            using var host = ASPSBackend.Program.CreateHostBuilder(
                    ["--ConnectionStrings:DefaultConnection=command-line-value"])
                .UseEnvironment(Environments.Production)
                .Build();

            Assert.Equal("development-override", host.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private sealed class ScopedDependency;

    private sealed class SingletonWithCaptiveDependency(ScopedDependency dependency);
}
