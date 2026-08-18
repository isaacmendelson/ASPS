using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApi.Services;

/// <summary>
/// Parses raw inbound <c>/ws/agent</c> WS text frames into <see cref="AgentFrame"/>,
/// enforcing the frame envelope rules from WS-AGENT-PROTOCOL.md section 3.1 and the
/// size limit from section 2. Pure/stateless — no IO, fully unit-testable.
/// </summary>
public static class AgentFrameParser
{
    public static AgentFrameParseResult Parse(string raw, int maxFrameSizeBytes)
    {
        if (raw is null)
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "Frame is null");

        var byteCount = Encoding.UTF8.GetByteCount(raw);
        if (byteCount > maxFrameSizeBytes)
            return AgentFrameParseResult.Fail(
                AgentErrorCode.FrameTooLarge,
                $"Frame size {byteCount} bytes exceeds the {maxFrameSizeBytes}-byte limit");

        JObject root;
        try
        {
            // DateParseHandling.None: the gateway forwards payload JSON verbatim to
            // Backend (WS-AGENT-PROTOCOL.md section 3.1). Newtonsoft's default date
            // auto-detection would silently rewrite ISO-8601-looking string values
            // (e.g. Timestamp fields) into JTokenType.Date, changing their formatting
            // when the payload is re-serialized. Keep every scalar a plain string.
            using var stringReader = new StringReader(raw);
            using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
            var token = JToken.ReadFrom(jsonReader);
            if (token is not JObject obj)
                return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "Frame must be a JSON object");
            root = obj;
        }
        catch (JsonException)
        {
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "Frame is not valid JSON");
        }

        var frameType = root["frame"]?.Type == JTokenType.String ? root["frame"]!.Value<string>() : null;
        if (string.IsNullOrEmpty(frameType))
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "Missing required 'frame' field");

        return frameType switch
        {
            AgentFrameType.Request => ParseRequest(root),
            AgentFrameType.Subscribe => ParseSubscribe(root),
            AgentFrameType.Pong => AgentFrameParseResult.Ok(new AgentFrame
            {
                Frame = AgentFrameType.Pong,
                Ts = root["ts"]?.Type == JTokenType.String ? root["ts"]!.Value<string>() : null
            }),
            _ => AgentFrameParseResult.Fail(AgentErrorCode.UnknownFrameType, $"Unrecognized frame type '{frameType}'")
        };
    }

    private static AgentFrameParseResult ParseRequest(JObject root)
    {
        var id = root["id"]?.Type == JTokenType.String ? root["id"]!.Value<string>() : null;
        if (string.IsNullOrEmpty(id))
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "'request' frame requires a string 'id'");

        if (root["payload"] is not JObject payload)
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "'request' frame requires an object 'payload'");

        return AgentFrameParseResult.Ok(new AgentFrame
        {
            Frame = AgentFrameType.Request,
            Id = id,
            Payload = payload
        });
    }

    private static AgentFrameParseResult ParseSubscribe(JObject root)
    {
        var deviceUid = root["deviceUid"]?.Type == JTokenType.String ? root["deviceUid"]!.Value<string>() : null;
        if (string.IsNullOrEmpty(deviceUid))
            return AgentFrameParseResult.Fail(AgentErrorCode.InvalidFrame, "'subscribe' frame requires a string 'deviceUid'");

        return AgentFrameParseResult.Ok(new AgentFrame
        {
            Frame = AgentFrameType.Subscribe,
            DeviceUid = deviceUid
        });
    }
}
