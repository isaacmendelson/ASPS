using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebApi.Services;

namespace WebApi.Middleware;

/// <summary>
/// ASP.NET Core middleware implementing the <c>/ws/agent</c> WebSocket gateway —
/// see <c>docs/architecture/WS-AGENT-PROTOCOL.md</c> and ADR-004 (ASPS-718/720).
/// Bridges desktop-agent WebSocket connections to Backend's ZMQ sockets on
/// localhost via <see cref="AgentGatewayService"/>.
///
/// MUST be registered before <c>UseAuthentication</c>/<c>UseAuthorization</c> in
/// Program.cs: this WebApi's authorization fallback policy requires an
/// authenticated Keycloak Admin — device agents authenticate at the application
/// layer instead (RequestToken/RefreshToken over the WS "request" frame, per
/// section 4), so /ws/agent must never be subject to the Admin fallback policy.
/// </summary>
public sealed class AgentWebSocketMiddleware
{
    private const string Path = "/ws/agent";
    private const string SubProtocol = "asps-agent-v1";

    private readonly RequestDelegate _next;
    private readonly ILogger<AgentWebSocketMiddleware> _logger;

    public AgentWebSocketMiddleware(RequestDelegate next, ILogger<AgentWebSocketMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(Path))
        {
            await _next(context);
            return;
        }

        // Resolved lazily, only for /ws/agent requests: AgentGatewayService's
        // constructor pulls in CurveKeyManager, which reads CURVE key material
        // from disk. Injecting it as a method parameter would force ASP.NET Core
        // to resolve (and construct) it for every request through this global
        // middleware, not just agent connections — breaking any environment
        // that hasn't provisioned CURVE keys, regardless of path.
        var gateway = context.RequestServices.GetRequiredService<AgentGatewayService>();

        if (!gateway.Options.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!context.WebSockets.WebSocketRequestedProtocols.Contains(SubProtocol))
        {
            _logger.LogWarning("Rejected /ws/agent upgrade — missing '{SubProtocol}' subprotocol", SubProtocol);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!gateway.TryAcquireConnectionSlot(clientIp))
        {
            _logger.LogWarning("Rejected /ws/agent upgrade — connection limit exceeded for {ClientIp}", clientIp);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        var connectionId = context.TraceIdentifier;
        try
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync(SubProtocol);
            _logger.LogInformation("Agent WS connection accepted {ConnectionId} from {ClientIp}", connectionId, clientIp);

            await RunConnectionAsync(webSocket, gateway, connectionId, context.RequestAborted);
        }
        finally
        {
            gateway.ReleaseConnectionSlot(clientIp);
        }
    }

    private async Task RunConnectionAsync(
        WebSocket webSocket, AgentGatewayService gateway, string connectionId, CancellationToken serverStopping)
    {
        var options = gateway.Options;
        var connection = new AgentConnection(
            gateway,
            (json, ct) => SendAsync(webSocket, json, ct),
            _logger,
            connectionId);

        var buffer = new byte[Math.Min(options.MaxFrameSize, 65536)];
        var idleTimeout = TimeSpan.FromSeconds(options.UnauthenticatedTimeoutSeconds);

        try
        {
            while (webSocket.State == WebSocketState.Open && !serverStopping.IsCancellationRequested)
            {
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(serverStopping);
                receiveCts.CancelAfter(idleTimeout);

                string? message;
                try
                {
                    message = await ReceiveTextMessageAsync(webSocket, buffer, options.MaxFrameSize, receiveCts.Token);
                }
                catch (OperationCanceledException) when (!serverStopping.IsCancellationRequested)
                {
                    _logger.LogInformation("Agent WS connection {ConnectionId} idle timeout — closing", connectionId);
                    var idleCloseStatus = connection.State == AgentConnectionState.Authenticated
                        ? WebSocketCloseStatus.NormalClosure
                        : (WebSocketCloseStatus)AgentCloseCode.AuthenticationRequired;
                    await CloseAsync(webSocket, idleCloseStatus, "idle timeout", serverStopping);
                    break;
                }
                catch (InvalidOperationException)
                {
                    // Raised by ReceiveTextMessageAsync when accumulated frame exceeds MaxFrameSize.
                    await SendAsync(webSocket, AgentFrameBuilder.BuildErrorFrame(
                        null, AgentErrorCode.FrameTooLarge, "Frame exceeds the maximum allowed size"), serverStopping);
                    await CloseAsync(webSocket, (WebSocketCloseStatus)AgentCloseCode.InvalidFrame,
                        AgentErrorCode.FrameTooLarge, serverStopping);
                    break;
                }

                if (message is null)
                    break; // remote closed

                var parsed = AgentFrameParser.Parse(message, options.MaxFrameSize);
                if (!parsed.Success)
                {
                    await SendAsync(webSocket,
                        AgentFrameBuilder.BuildErrorFrame(null, parsed.ErrorCode!, parsed.ErrorMessage!), serverStopping);
                    await CloseAsync(webSocket, (WebSocketCloseStatus)AgentCloseCode.InvalidFrame,
                        parsed.ErrorCode, serverStopping);
                    break;
                }

                await connection.HandleFrameAsync(parsed.Frame!, serverStopping);

                idleTimeout = connection.State == AgentConnectionState.Authenticated
                    ? TimeSpan.FromSeconds(options.AuthenticatedIdleTimeoutSeconds)
                    : TimeSpan.FromSeconds(options.UnauthenticatedTimeoutSeconds);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogInformation(ex, "Agent WS connection {ConnectionId} closed unexpectedly", connectionId);
        }
        finally
        {
            connection.Dispose();
            _logger.LogInformation("Agent WS connection {ConnectionId} closed, device={DeviceUid}",
                connectionId, connection.DeviceUid ?? "(unauthenticated)");
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(
        WebSocket webSocket, byte[] buffer, int maxFrameSize, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);
            if (ms.Length > maxFrameSize)
                throw new InvalidOperationException("frame too large");
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static Task SendAsync(WebSocket webSocket, string json, CancellationToken ct)
    {
        if (webSocket.State != WebSocketState.Open)
            return Task.CompletedTask;
        var bytes = Encoding.UTF8.GetBytes(json);
        return webSocket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static async Task CloseAsync(WebSocket webSocket, WebSocketCloseStatus status, string? description, CancellationToken ct)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.CloseAsync(status, description, ct);
            }
            catch
            {
                // best effort — connection is going away regardless
            }
        }
    }
}
