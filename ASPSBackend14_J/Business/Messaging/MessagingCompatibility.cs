namespace Business.Messaging;

using Newtonsoft.Json.Linq;

public sealed class MessagingCompatibilityOptions
{
    public bool AcceptLegacyV0 { get; init; }
}

public sealed record MessagingNegotiationResult(int SchemaMajor, bool LegacyAdapted)
{
    public bool CanPerformTabTargetedAction => SchemaMajor == 1 && !LegacyAdapted;
}

public sealed class MessagingCompatibilityException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public static class MessagingCompatibility
{
    public static readonly int[] SupportedSchemaMajors = [1];

    public static MessagingNegotiationResult Negotiate(
        IEnumerable<int> peerSupportedSchemaMajors,
        MessagingCompatibilityOptions options)
    {
        var peer = peerSupportedSchemaMajors.Distinct().ToHashSet();
        if (peer.Contains(1))
            return new MessagingNegotiationResult(1, false);
        if (options.AcceptLegacyV0 && peer.Contains(0))
            return new MessagingNegotiationResult(0, true);

        throw new MessagingCompatibilityException(
            "protocol.unsupported_schema_version",
            "No mutually supported messaging schema major.");
    }

    public static void AdaptLegacyIngress(
        JObject alert,
        MessagingCompatibilityOptions options)
    {
        if (!options.AcceptLegacyV0)
            throw new MessagingCompatibilityException(
                "protocol.unsupported_schema_version",
                "Legacy messaging v0 is disabled.");
        if (alert.Property("TabId", StringComparison.OrdinalIgnoreCase) is { } tabId)
            tabId.Value = JValue.CreateNull();
        alert["LegacyAdapted"] = true;
    }
}
