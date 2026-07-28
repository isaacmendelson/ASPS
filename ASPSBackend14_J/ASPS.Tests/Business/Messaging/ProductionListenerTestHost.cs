using Business.RealtimeAnalysis.UserDomain;
using Business.Services;
using Business.Views;
using Common.Entities;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ASPS.Tests.Business.Messaging;

internal static class ProductionListenerTestHost
{
    public static IHost Build(bool acceptLegacyV0)
    {
        return ASPSBackend.Program.CreateHostBuilder(
            [
                $"--Messaging:AcceptLegacyV0={acceptLegacyV0}",
                "--ConnectionStrings:DefaultConnection=Server=127.0.0.1;Database=asps_test;User=test;Password=test"
            ])
            .ConfigureServices(services =>
            {
                services.AddLogging(builder => builder.ClearProviders());
                services.AddSingleton(sp =>
                {
                    var view = new ASView(
                        sp,
                        sp.GetRequiredService<ILogger<ASView>>(),
                        sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
                    SetPrivateList(view, "_users", new User
                    {
                        KeyField = "user-1",
                        Email = "runtime@example.test"
                    });
                    SetPrivateList<UserDevice>(view, "_userDevices", new PersonalComputer
                    {
                        KeyField = "device-key-1",
                        UserKeyField = "user-1",
                        DeviceUid = "device-1"
                    });
                    return view;
                });
                services.AddSingleton(
                    (UserDomainManagerService)RuntimeHelpers.GetUninitializedObject(
                        typeof(UserDomainManagerService)));
                services.AddSingleton(sp => new TokenStore(
                    sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                    sp.GetRequiredService<ILogger<TokenStore>>(),
                    sp));
                services.AddSingleton(
                    (CurveKeyManager)RuntimeHelpers.GetUninitializedObject(
                        typeof(CurveKeyManager)));
                services.AddScoped<IDeviceAlertRepository>(
                    _ => Mock.Of<IDeviceAlertRepository>());
            })
            .Build();
    }

    private static void SetPrivateList<T>(ASView view, string fieldName, T value)
    {
        typeof(ASView).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(view, new List<T> { value });
    }
}
