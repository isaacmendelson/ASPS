using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace WebApi.Pages;

public class DeviceLoginModel : PageModel
{
    // Z85 character set for decoding
    private const string Z85Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

    private static byte[] DecodeZ85(string encoded)
    {
        if (encoded.Length % 5 != 0)
            throw new ArgumentException("Encoded string length must be a multiple of 5");

        var data = new byte[encoded.Length * 4 / 5];

        for (int i = 0; i < encoded.Length; i += 5)
        {
            uint value = 0;
            for (int j = 0; j < 5; j++)
            {
                value = value * 85 + (uint)Z85Chars.IndexOf(encoded[i + j]);
            }

            int idx = (i / 5) * 4;
            data[idx] = (byte)((value >> 24) & 0xFF);
            data[idx + 1] = (byte)((value >> 16) & 0xFF);
            data[idx + 2] = (byte)((value >> 8) & 0xFF);
            data[idx + 3] = (byte)(value & 0xFF);
        }

        return data;
    }

    private readonly ILogger<DeviceLoginModel> _logger;
    private readonly string _alertListenerEndpoint;
    private readonly bool _curveEnabled;
    private readonly string? _serverPublicKeyZ85;

    [BindProperty(SupportsGet = true)]
    public string DeviceUid { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public DeviceLoginModel(IConfiguration configuration, ILogger<DeviceLoginModel> logger)
    {
        _logger = logger;
        _alertListenerEndpoint = configuration.GetValue<string>("NetMQ:AlertListenerEndpoint")
            ?? "tcp://localhost:50001";
        _curveEnabled = configuration.GetValue<bool>("Security:CurveEnabled", false);
        _serverPublicKeyZ85 = configuration.GetValue<string>("Security:ServerPublicKeyZ85");
    }

    public void OnGet()
    {
        if (string.IsNullOrEmpty(DeviceUid))
        {
            ErrorMessage = "No device ID provided. Please use the link from your desktop app.";
        }
    }

    public void OnPost()
    {
        if (string.IsNullOrEmpty(DeviceUid) || string.IsNullOrEmpty(Email))
        {
            ErrorMessage = "Device ID and email are required.";
            return;
        }

        try
        {
            _logger.LogInformation("Device login attempt: DeviceUid={DeviceUid}, Email={Email}", DeviceUid, Email);

            var registerMessage = new
            {
                MessageType = "RegisterDevice",
                DeviceUid = DeviceUid,
                Email = Email,
                DeviceType = 1, // PersonalComputer
                OperatingSystem = 1 // Windows
            };

            var response = SendToBackend(registerMessage);

            if (response == null)
            {
                ErrorMessage = "Could not reach the backend server. Please try again.";
                return;
            }

            var status = response["status"]?.ToString();

            switch (status)
            {
                case "Registered":
                    SuccessMessage = "Device registered successfully! You can close this page and return to the desktop app.";
                    _logger.LogInformation("Device {DeviceUid} registered via login page", DeviceUid);
                    break;
                case "InvalidUser":
                    ErrorMessage = "No active account found for this email address.";
                    break;
                default:
                    ErrorMessage = response["message"]?.ToString() ?? "Registration failed. Please try again.";
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device login");
            ErrorMessage = "An error occurred during registration. Please try again.";
        }
    }

    private JObject? SendToBackend(object message)
    {
        _logger.LogInformation("SendToBackend: Endpoint={Endpoint}, CurveEnabled={CurveEnabled}, ServerKey={ServerKey}",
            _alertListenerEndpoint, _curveEnabled, _serverPublicKeyZ85?.Substring(0, Math.Min(10, _serverPublicKeyZ85?.Length ?? 0)) + "...");

        try
        {
            using var socket = new RequestSocket();
            socket.Options.Linger = TimeSpan.Zero;

            // Configure CURVE encryption if enabled
            if (_curveEnabled && !string.IsNullOrEmpty(_serverPublicKeyZ85))
            {
                _logger.LogInformation("Configuring CURVE encryption for backend connection");

                // Generate client keypair and set certificate
                var clientCert = new NetMQCertificate();
                socket.Options.CurveCertificate = clientCert;
                socket.Options.CurveServerKey = DecodeZ85(_serverPublicKeyZ85);
                _logger.LogInformation("CURVE configured. Client public key: {Key}", clientCert.PublicKeyZ85?.Substring(0, 10) + "...");
            }
            else
            {
                _logger.LogWarning("CURVE NOT enabled! CurveEnabled={CurveEnabled}, HasServerKey={HasKey}",
                    _curveEnabled, !string.IsNullOrEmpty(_serverPublicKeyZ85));
            }

            _logger.LogInformation("Connecting to {Endpoint}...", _alertListenerEndpoint);
            socket.Connect(_alertListenerEndpoint);

            var json = JsonConvert.SerializeObject(message);
            _logger.LogInformation("Sending message: {Message}", json);
            var sent = socket.TrySendFrame(TimeSpan.FromSeconds(5), json);

            if (!sent)
            {
                _logger.LogError("Failed to send registration message - send timeout after 5 seconds");
                return null;
            }

            _logger.LogInformation("Message sent, waiting for response...");
            string? responseJson;
            var received = socket.TryReceiveFrameString(TimeSpan.FromSeconds(10), out responseJson);

            if (!received || string.IsNullOrEmpty(responseJson))
            {
                _logger.LogError("No response from backend - receive timeout after 10 seconds");
                return null;
            }

            _logger.LogInformation("Response received: {Response}", responseJson);
            return JObject.Parse(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with backend: {Message}", ex.Message);
            return null;
        }
    }
}
