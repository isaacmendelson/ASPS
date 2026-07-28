using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetMQ;
using System.Text;
using System.Text.Json;

namespace Business.Services;

/// <summary>
/// Manages CurveZMQ server keypair for encrypted ZMQ communication.
///
/// Key lifecycle:
///   1. On startup, look for a keys file at Security:KeysFilePath
///      (default: %LOCALAPPDATA%\ASPS\curve-server-keys.json on Windows,
///                ~/.asps/curve-server-keys.json on Linux/macOS).
///   2. If found, load the existing keypair.
///   3. If not found, generate a new keypair, save it to the file, and log
///      the public key so clients can be configured.
///
/// The keys file is NEVER committed to git — it lives outside the repo.
/// Only the server public key (Z85) needs to be distributed to clients.
/// </summary>
public class CurveKeyManager
{
    private readonly ILogger<CurveKeyManager> _logger;
    private readonly bool _curveEnabled;
    private readonly string _keysFilePath;
    private readonly string _publicKeyFilePath;
    private readonly bool _clientOnly;

    public byte[] ServerPublicKey { get; private set; } = Array.Empty<byte>();
    public byte[] ServerSecretKey { get; private set; } = Array.Empty<byte>();
    public string ServerPublicKeyZ85 { get; private set; } = string.Empty;

    public CurveKeyManager(IConfiguration configuration, ILogger<CurveKeyManager> logger)
    {
        _logger = logger;
        _curveEnabled = configuration.GetValue<bool>("Security:CurveEnabled", true);
        _keysFilePath = ResolveKeysFilePath(configuration.GetValue<string>("Security:KeysFilePath"));
        _publicKeyFilePath = configuration.GetValue<string>("Security:ServerPublicKeyFilePath")
            ?? Path.Combine(Path.GetDirectoryName(_keysFilePath)!, "curve-server-public-key.txt");
        _clientOnly = configuration.GetValue<bool>("Security:CurveClientOnly", false);

        if (_curveEnabled)
        {
            if (_clientOnly)
                LoadClientPublicKey();
            else
                LoadOrGenerateKeys();
        }
        else
        {
            _logger.LogInformation("CurveZMQ encryption is disabled");
            // Clear the public key file so clients know CURVE is off.
            // Without this, a stale file from a previous CURVE-enabled run
            // causes clients to attempt CURVE → backend ignores → timeout.
            ClearPublicKeyFile();
        }
    }

    public bool IsEnabled => _curveEnabled;
    public bool HasServerKeyPair =>
        _curveEnabled && ServerPublicKey.Length == 32 && ServerSecretKey.Length == 32;

    /// <summary>Apply CURVE server options to a REP/PUB socket.</summary>
    public void ApplyServerCurve(NetMQSocket socket)
    {
        if (!_curveEnabled)
            throw new InvalidOperationException("CURVE encryption is disabled.");
        if (!HasServerKeyPair)
            throw new InvalidOperationException("Valid CURVE server public and private keys are required.");

        socket.Options.CurveServer = true;
        socket.Options.CurveCertificate = new NetMQCertificate(ServerSecretKey, ServerPublicKey);
        _logger.LogDebug("CURVE server mode applied to socket");
    }

    /// <summary>Apply CURVE client options to a REQ/SUB socket.</summary>
    public void ApplyClientCurve(NetMQSocket socket)
    {
        if (!_curveEnabled || ServerPublicKey.Length == 0) return;

        var clientCert = new NetMQCertificate();
        socket.Options.CurveCertificate = clientCert;
        socket.Options.CurveServerKey = ServerPublicKey;
        _logger.LogDebug("CURVE client mode applied to socket");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadOrGenerateKeys()
    {
        if (TryLoadKeysFromFile())
            return;

        _logger.LogWarning("CURVE keys not found at {Path} — generating new keypair", _keysFilePath);
        GenerateAndSaveKeys();
    }

    private void LoadClientPublicKey()
    {
        if (!File.Exists(_publicKeyFilePath))
            throw new InvalidOperationException(
                $"CURVE server public key was not found at {_publicKeyFilePath}.");

        ServerPublicKeyZ85 = File.ReadAllText(_publicKeyFilePath).Trim();
        if (ServerPublicKeyZ85.Length != 40)
            throw new InvalidOperationException("CURVE server public key must be a 40-character Z85 value.");
        ServerPublicKey = Z85.Decode(ServerPublicKeyZ85);
        ServerSecretKey = Array.Empty<byte>();
        _logger.LogInformation("CURVE server public key loaded for client use from {Path}", _publicKeyFilePath);
    }

    private bool TryLoadKeysFromFile()
    {
        if (!File.Exists(_keysFilePath))
            return false;

        try
        {
            var json = File.ReadAllText(_keysFilePath);
            var data = JsonSerializer.Deserialize<CurveKeyFile>(json);

            if (data is null || string.IsNullOrEmpty(data.ServerPublicKey) ||
                string.IsNullOrEmpty(data.ServerSecretKey))
            {
                _logger.LogWarning("Keys file at {Path} is missing or incomplete", _keysFilePath);
                return false;
            }

            ServerPublicKey   = Convert.FromBase64String(data.ServerPublicKey);
            ServerSecretKey   = Convert.FromBase64String(data.ServerSecretKey);
            ServerPublicKeyZ85 = data.ServerPublicKeyZ85 ?? Z85.Encode(ServerPublicKey);

            _logger.LogInformation("CurveZMQ keypair loaded from {Path}", _keysFilePath);
            _logger.LogInformation("Server public key (Z85): {Key}", ServerPublicKeyZ85);
            SavePublicKeyFile();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load CURVE keys from {Path}", _keysFilePath);
            return false;
        }
    }

    private void GenerateAndSaveKeys()
    {
        var cert = new NetMQCertificate();
        ServerPublicKey    = cert.PublicKey;
        ServerSecretKey    = cert.SecretKey;
        ServerPublicKeyZ85 = Z85.Encode(ServerPublicKey);

        var data = new CurveKeyFile
        {
            ServerPublicKey    = Convert.ToBase64String(ServerPublicKey),
            ServerSecretKey    = Convert.ToBase64String(ServerSecretKey),
            ServerPublicKeyZ85 = ServerPublicKeyZ85,
            GeneratedAt        = DateTime.UtcNow.ToString("o")
        };

        try
        {
            var dir = Path.GetDirectoryName(_keysFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_keysFilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            // Restrict file permissions on Linux/macOS
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_keysFilePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            _logger.LogInformation("CURVE keypair saved to {Path}", _keysFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save CURVE keys to {Path} — keys are in memory only", _keysFilePath);
        }

        SavePublicKeyFile();

        _logger.LogWarning("════════════════════════════════════════════════════════");
        _logger.LogWarning("NEW CURVE KEYPAIR GENERATED");
        _logger.LogWarning("Distribute this public key (Z85) to all desktop clients:");
        _logger.LogWarning("  {Key}", ServerPublicKeyZ85);
        _logger.LogWarning("Set BACKEND_SERVER_PUBLIC_KEY_Z85 in the desktop app config.");
        _logger.LogWarning("════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Write the server public key (Z85) to a separate, world-readable file.
    /// Python and other clients read from this file — they never need access
    /// to the full keypair file that contains the private key.
    /// </summary>
    private void SavePublicKeyFile()
    {
        try
        {
            File.WriteAllText(_publicKeyFilePath, ServerPublicKeyZ85);
            _logger.LogInformation("CURVE public key written to {Path}", _publicKeyFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write CURVE public key file to {Path}", _publicKeyFilePath);
        }
    }

    /// <summary>
    /// Clear the public key file when CURVE is disabled.
    /// Clients check this file to decide whether to use CURVE — an empty file
    /// signals "no encryption", preventing timeout from stale key files.
    /// </summary>
    private void ClearPublicKeyFile()
    {
        try
        {
            if (File.Exists(_publicKeyFilePath))
            {
                File.WriteAllText(_publicKeyFilePath, string.Empty);
                _logger.LogInformation("CURVE public key file cleared (CURVE disabled): {Path}", _publicKeyFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clear CURVE public key file at {Path}", _publicKeyFilePath);
        }
    }

    private static string ResolveKeysFilePath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // Default: platform-appropriate app-data directory
        var baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Combine(baseDir, "ASPS", "curve-server-keys.json");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class CurveKeyFile
    {
        public string ServerPublicKey    { get; init; } = string.Empty;
        public string ServerSecretKey    { get; init; } = string.Empty;
        public string? ServerPublicKeyZ85 { get; init; }
        public string? GeneratedAt       { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Z85 encoding (ZMQ standard for public key distribution)
    // ─────────────────────────────────────────────────────────────────────────

    private static class Z85
    {
        private const string Chars =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";

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
                    sb.Insert(sb.Length - (4 - j), Chars[(int)(value % 85)]);
                    value /= 85;
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
                    value = value * 85 + (uint)Chars.IndexOf(encoded[i + j]);

                int idx = (i / 5) * 4;
                data[idx]     = (byte)((value >> 24) & 0xFF);
                data[idx + 1] = (byte)((value >> 16) & 0xFF);
                data[idx + 2] = (byte)((value >> 8)  & 0xFF);
                data[idx + 3] = (byte)(value          & 0xFF);
            }
            return data;
        }
    }
}
