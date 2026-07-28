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

    [Fact]
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
