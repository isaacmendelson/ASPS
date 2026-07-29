using Business.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Entities;
using Common.Enums;

namespace ASPS.Tests.Business.Messaging;

public class CqrsChannelSecurityTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Constructor_RejectsMissingOrShortSecrets()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CqrsChannelSecurity(new CqrsChannelSecurityOptions()));
        Assert.Throws<InvalidOperationException>(() =>
            new CqrsChannelSecurity(new CqrsChannelSecurityOptions { SharedSecret = "short" }));
    }

    [Fact]
    public void TryUnprotect_AcceptsValidAuthenticatedEnvelope()
    {
        var client = CreateSecurity();
        var server = CreateSecurity();
        var protectedJson = client.Protect("""{"MessageType":"Query","QueryType":"GetVersionQuery"}""");

        var accepted = server.TryUnprotect(protectedJson, out var payload, out var clientId, out var error);

        Assert.True(accepted, error);
        Assert.Contains("GetVersionQuery", payload);
        Assert.Equal("asps-webapi", clientId);
    }

    [Fact]
    public void TryUnprotect_RejectsTamperedPayload()
    {
        var client = CreateSecurity();
        var server = CreateSecurity();
        var envelope = JObject.Parse(client.Protect("""{"MessageType":"Query","QueryType":"GetVersionQuery"}"""));
        envelope["Payload"] = """{"MessageType":"Command","CommandType":"DeleteUserCommand"}""";

        Assert.False(server.TryUnprotect(envelope.ToString(Formatting.None), out _, out _, out _));
    }

    [Fact]
    public void TryUnprotect_RejectsUnknownClient()
    {
        var attacker = CreateSecurity("attacker");
        var server = CreateSecurity();

        Assert.False(server.TryUnprotect(attacker.Protect("{}"), out _, out _, out var error));
        Assert.Contains("not authorized", error);
    }

    [Fact]
    public void TryUnprotect_RejectsReplay()
    {
        var client = CreateSecurity();
        var server = CreateSecurity();
        var protectedJson = client.Protect("{}");

        Assert.True(server.TryUnprotect(protectedJson, out _, out _, out _));
        Assert.False(server.TryUnprotect(protectedJson, out _, out _, out var error));
        Assert.Contains("already been used", error);
    }

    [Fact]
    public void TryUnprotect_RejectsTypeMetadataWithoutInstantiatingIt()
    {
        var server = CreateSecurity();
        var malicious = """{"$type":"System.IO.FileInfo, System.Private.CoreLib","OriginalPath":"secret.txt"}""";

        Assert.False(server.TryUnprotect(malicious, out _, out _, out _));
    }

    [Fact]
    public void CommandAuthorization_IsExplicitAllowlist()
    {
        var security = CreateSecurity();

        Assert.True(security.IsCommandAuthorized("UpdateUserCommand"));
        Assert.False(security.IsCommandAuthorized("ReInitializeASViewCommand"));
    }

    [Fact]
    public void SafeSerialization_UsesExplicitAlertDiscriminatorWithoutTypeMetadata()
    {
        var alerts = new List<DeviceAlertEntity>
        {
            new UrlAlertEntity { AlertType = "UrlAlert", Url = "https://example.test" },
            new RemoteAccessAlertEntity { AlertType = "RemoteAccessAlert" }
        };
        var json = JsonConvert.SerializeObject(alerts, CqrsJsonSerialization.CreateSettings());

        var result = JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(
            json, CqrsJsonSerialization.CreateSettings());

        Assert.DoesNotContain("$type", json);
        Assert.IsType<UrlAlertEntity>(result![0]);
        Assert.IsType<RemoteAccessAlertEntity>(result[1]);
    }

    [Fact]
    public void SafeSerialization_RejectsUnknownPolymorphicDiscriminator()
    {
        var json = """[{"AlertType":"InjectedAlert"}]""";

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<List<DeviceAlertEntity>>(
                json, CqrsJsonSerialization.CreateSettings()));
    }

    [Fact]
    public void UserDeviceConverter_DeserializesInt64DeviceType()
    {
        // Regression: JObject stores integers as Int64; direct Value<DeviceType?> throws
        // "Invalid cast from 'System.Int64' to 'Common.Enums.DeviceType'"
        var json = """{"DeviceType":1,"DeviceUid":"pc-1","Make":"Dell"}""";

        var device = JsonConvert.DeserializeObject<UserDevice>(
            json, CqrsJsonSerialization.CreateSettings());

        Assert.NotNull(device);
        Assert.IsType<PersonalComputer>(device);
        Assert.Equal(DeviceType.PersonalComputer, device!.DeviceType);
        Assert.Equal("pc-1", device.DeviceUid);
    }

    [Fact]
    public void UserDeviceConverter_DeserializesUnknownDeviceType()
    {
        // DeviceType.Unknown (0) and DeviceType.Other (3) must not throw
        var json = """{"DeviceType":0,"DeviceUid":"unknown-1"}""";

        var device = JsonConvert.DeserializeObject<UserDevice>(
            json, CqrsJsonSerialization.CreateSettings());

        Assert.NotNull(device);
        Assert.Equal(DeviceType.Unknown, device!.DeviceType);
    }

    [Fact]
    public void UserDeviceConverter_DeserializesOtherDeviceType()
    {
        var json = """{"DeviceType":3,"DeviceUid":"other-1"}""";

        var device = JsonConvert.DeserializeObject<UserDevice>(
            json, CqrsJsonSerialization.CreateSettings());

        Assert.NotNull(device);
        Assert.Equal(DeviceType.Other, device!.DeviceType);
    }

    [Fact]
    public void UserDeviceConverter_RoundTripsDeviceList()
    {
        var devices = new List<UserDevice>
        {
            new PersonalComputer { DeviceUid = "pc-1", DeviceType = DeviceType.PersonalComputer },
            new SmartPhone { DeviceUid = "phone-1", DeviceType = DeviceType.MobilePhone }
        };
        var json = JsonConvert.SerializeObject(devices, CqrsJsonSerialization.CreateSettings());

        var result = JsonConvert.DeserializeObject<List<UserDevice>>(
            json, CqrsJsonSerialization.CreateSettings());

        Assert.Equal(2, result!.Count);
        Assert.IsType<PersonalComputer>(result[0]);
        Assert.IsType<SmartPhone>(result[1]);
    }

    private static CqrsChannelSecurity CreateSecurity(string clientId = "asps-webapi") =>
        new(new CqrsChannelSecurityOptions
        {
            ClientId = clientId,
            SharedSecret = Secret,
            AllowedClientIds = new HashSet<string>(StringComparer.Ordinal) { "asps-webapi" },
            AllowedCommands = new HashSet<string>(StringComparer.Ordinal) { "UpdateUserCommand" }
        });
}
