using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetMQ;
using System.Text;

namespace Business.Services;

/// <summary>
/// Manages CurveZMQ server keypair for encrypted ZMQ communication.
/// Keys are loaded from appsettings.json configuration.
/// </summary>
public class CurveKeyManager
{
    private readonly ILogger<CurveKeyManager> _logger;
    private readonly bool _curveEnabled;

    public byte[] ServerPublicKey { get; private set; } = Array.Empty<byte>();
    public byte[] ServerSecretKey { get; private set; } = Array.Empty<byte>();
    public string ServerPublicKeyZ85 { get; private set; } = string.Empty;

    public CurveKeyManager(IConfiguration configuration, ILogger<CurveKeyManager> logger)
    {
        _logger = logger;
        _curveEnabled = configuration.GetValue<bool>("Security:CurveEnabled", true);

        if (_curveEnabled)
        {
            LoadKeysFromConfig(configuration);
        }
        else
        {
            _logger.LogInformation("CurveZMQ encryption is disabled");
        }
    }

    public bool IsEnabled => _curveEnabled;

    /// <summary>
    /// Apply CURVE server options to a socket (for REP/PUB sockets).
    /// </summary>
    public void ApplyServerCurve(NetMQSocket socket)
    {
        if (!_curveEnabled || ServerSecretKey.Length == 0)
            return;

        socket.Options.CurveServer = true;
        socket.Options.CurveCertificate = new NetMQCertificate(ServerSecretKey, ServerPublicKey);
        _logger.LogDebug("CURVE server mode applied to socket");
    }

    private void LoadKeysFromConfig(IConfiguration configuration)
    {
        var publicKeyBase64 = configuration.GetValue<string>("Security:ServerPublicKey");
        var secretKeyBase64 = configuration.GetValue<string>("Security:ServerSecretKey");
        var publicKeyZ85 = configuration.GetValue<string>("Security:ServerPublicKeyZ85");

        if (string.IsNullOrEmpty(publicKeyBase64) || string.IsNullOrEmpty(secretKeyBase64))
        {
            _logger.LogWarning("CURVE keys not found in configuration. Generating new keypair...");
            GenerateKeys();
            LogKeyConfiguration();
            return;
        }

        try
        {
            ServerPublicKey = Convert.FromBase64String(publicKeyBase64);
            ServerSecretKey = Convert.FromBase64String(secretKeyBase64);
            ServerPublicKeyZ85 = publicKeyZ85 ?? Z85.Encode(ServerPublicKey);

            _logger.LogInformation("CurveZMQ keypair loaded from configuration");
            _logger.LogInformation("Server public key (Z85): {PublicKey}", ServerPublicKeyZ85);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CURVE keys from configuration. Generating new keypair...");
            GenerateKeys();
            LogKeyConfiguration();
        }
    }

    private void GenerateKeys()
    {
        _logger.LogInformation("Generating new CurveZMQ keypair...");

        var cert = new NetMQCertificate();
        ServerPublicKey = cert.PublicKey;
        ServerSecretKey = cert.SecretKey;
        ServerPublicKeyZ85 = Z85.Encode(ServerPublicKey);

        _logger.LogInformation("Server public key (Z85): {PublicKey}", ServerPublicKeyZ85);
    }

    private void LogKeyConfiguration()
    {
        _logger.LogWarning("Add these keys to your appsettings.json Security section:");
        _logger.LogWarning("  \"ServerPublicKey\": \"{PublicKey}\"", Convert.ToBase64String(ServerPublicKey));
        _logger.LogWarning("  \"ServerSecretKey\": \"{SecretKey}\"", Convert.ToBase64String(ServerSecretKey));
        _logger.LogWarning("  \"ServerPublicKeyZ85\": \"{PublicKeyZ85}\"", ServerPublicKeyZ85);
    }

    /// <summary>
    /// Z85 encoding for CurveZMQ public keys (standard ZMQ key encoding).
    /// </summary>
    private static class Z85
    {
        private const string Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

        public static string Encode(byte[] data)
        {
            if (data.Length % 4 != 0)
                throw new ArgumentException("Data length must be a multiple of 4");

            var sb = new StringBuilder(data.Length * 5 / 4);

            for (int i = 0; i < data.Length; i += 4)
            {
                uint value = ((uint)data[i] << 24) | ((uint)data[i + 1] << 16) |
                             ((uint)data[i + 2] << 8) | data[i + 3];

                for (int j = 4; j >= 0; j--)
                {
                    var idx = value % 85;
                    value /= 85;
                    sb.Insert(sb.Length - (4 - j), Chars[(int)idx]);
                }
            }

            return sb.ToString();
        }

        public static byte[] Decode(string encoded)
        {
            if (encoded.Length % 5 != 0)
                throw new ArgumentException("Encoded string length must be a multiple of 5");

            var data = new byte[encoded.Length * 4 / 5];

            for (int i = 0; i < encoded.Length; i += 5)
            {
                uint value = 0;
                for (int j = 0; j < 5; j++)
                {
                    value = value * 85 + (uint)Chars.IndexOf(encoded[i + j]);
                }

                int idx = (i / 5) * 4;
                data[idx] = (byte)((value >> 24) & 0xFF);
                data[idx + 1] = (byte)((value >> 16) & 0xFF);
                data[idx + 2] = (byte)((value >> 8) & 0xFF);
                data[idx + 3] = (byte)(value & 0xFF);
            }

            return data;
        }
    }
}
