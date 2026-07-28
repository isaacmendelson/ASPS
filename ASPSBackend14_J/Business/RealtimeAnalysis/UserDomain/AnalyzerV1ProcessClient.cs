using Common.Generated.Messaging.V1;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Business.RealtimeAnalysis.UserDomain;

public sealed class AnalyzerV1ProcessException(
    string code,
    string message,
    MessageEnvelopeV1 response,
    MessageEnvelopeV1 request) : Exception(message)
{
    public string Code { get; } = code;
    public MessageEnvelopeV1 Response { get; } = response;
    public MessageEnvelopeV1 Request { get; } = request;
}

public sealed class AnalyzerV1ProcessClient
{
    public async Task<MessageEnvelopeV1> RunAsync(
        string pythonExecutable,
        string analyzerDirectory,
        MessageIdentityV1 identity,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var request = new MessageEnvelopeV1
        {
            // This is a new Backend→Analyzer wire emission. Preserve workflow
            // identity, but never reuse the upstream hop's messageId.
            MessageId = Guid.NewGuid(),
            CorrelationId = identity.CorrelationId,
            RequestId = identity.RequestId,
            MessageType = "url_scan.request",
            SentAt = DateTimeOffset.UtcNow,
            Source = "backend",
            Context = new MessageContextV1
            {
                DeviceId = identity.DeviceId,
                TabId = identity.TabId,
                Url = identity.Url
            },
            Outcome = null,
            Payload = JsonSerializer.SerializeToElement(new { })
        };
        var requestValidation = MessageEnvelopeValidator.Validate(request, requireDeviceId: true);
        if (!requestValidation.IsValid)
            throw new InvalidOperationException($"{requestValidation.ErrorCode}: {requestValidation.ErrorMessage}");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = "\"analyze.py\" --contract-version 1",
            WorkingDirectory = analyzerDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var requestNode = JsonSerializer.SerializeToNode(request)!.AsObject();
        requestNode["sentAt"] = request.SentAt.UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            System.Globalization.CultureInfo.InvariantCulture);
        await process.StandardInput.WriteAsync(requestNode.ToJsonString());
        process.StandardInput.Close();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw new TimeoutException("Analyzer v1 subprocess timed out.");
        }

        var stdout = await stdoutTask;
        _ = await stderrTask;
        var validation = MessageEnvelopeValidator.DeserializeAndValidate(
            stdout, out var response, requireDeviceId: true);
        if (!validation.IsValid || response is null)
            throw new InvalidOperationException(
                $"{validation.ErrorCode}: Analyzer returned an invalid v1 envelope.");
        var echo = MessageEnvelopeValidator.ValidateEcho(request, response);
        if (!echo.IsValid)
            throw new InvalidOperationException($"{echo.ErrorCode}: {echo.ErrorMessage}");
        if (response.Source != "analyzer" ||
            response.MessageType is not ("url_scan.result" or "url_scan.error"))
            throw new InvalidOperationException("protocol.unsupported_discriminator: Invalid analyzer response.");

        if (response.MessageType == "url_scan.error")
        {
            throw new AnalyzerV1ProcessException(
                response.Outcome!.Error!.Code,
                response.Outcome.Error.Message,
                response,
                request);
        }
        return response;
    }
}
