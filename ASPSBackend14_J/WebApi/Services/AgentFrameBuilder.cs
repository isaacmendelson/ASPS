using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApi.Services;

/// <summary>
/// Builds outbound <c>/ws/agent</c> WS text frames per WS-AGENT-PROTOCOL.md section 3.2.
/// Pure/stateless — no IO, fully unit-testable.
/// </summary>
public static class AgentFrameBuilder
{
    public static string BuildResponseFrame(string id, string payloadJson)
    {
        var obj = new JObject
        {
            ["frame"] = AgentFrameType.Response,
            ["id"] = id,
            ["payload"] = ParsePayload(payloadJson)
        };
        return obj.ToString(Formatting.None);
    }

    public static string BuildNotificationFrame(string topic, string payloadJson)
    {
        var obj = new JObject
        {
            ["frame"] = AgentFrameType.Notification,
            ["topic"] = topic,
            ["payload"] = ParsePayload(payloadJson)
        };
        return obj.ToString(Formatting.None);
    }

    public static string BuildErrorFrame(string? id, string code, string message)
    {
        var obj = new JObject
        {
            ["frame"] = AgentFrameType.Error,
            ["id"] = id,
            ["code"] = code,
            ["message"] = message
        };
        return obj.ToString(Formatting.None);
    }

    public static string BuildPingFrame(DateTime timestampUtc)
    {
        var obj = new JObject
        {
            ["frame"] = AgentFrameType.Ping,
            ["ts"] = timestampUtc.ToString("o")
        };
        return obj.ToString(Formatting.None);
    }

    /// <summary>
    /// Backend payloads are already-serialized JSON text. Embed them as a real JSON
    /// value (not a doubly-escaped string) so the agent receives the same structure
    /// it would over ZMQ. Falls back to a JSON string if Backend ever returns
    /// something that isn't valid JSON, so the agent still gets a frame back.
    /// </summary>
    private static JToken ParsePayload(string payloadJson)
    {
        try
        {
            // DateParseHandling.None: embed Backend's response verbatim. Without this,
            // Newtonsoft's date auto-detection rewrites ISO-8601-looking string fields
            // (e.g. "expiration", "timestamp") into a different textual format.
            using var stringReader = new StringReader(payloadJson);
            using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
            return JToken.ReadFrom(jsonReader);
        }
        catch (JsonException)
        {
            return JValue.CreateString(payloadJson);
        }
    }
}
