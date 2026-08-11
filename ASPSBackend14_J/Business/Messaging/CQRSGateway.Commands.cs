using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Business.Messaging;

/// <summary>
/// CQRS Gateway — command dispatch. Authorization stays here (gateway-level
/// concern); actual handler dispatch is delegated to CqrsHandlerRegistry
/// (ASPS-679/ASPS-681 Messaging Refactoring, Phase 1).
/// </summary>
public partial class CQRSGateway
{
    private async Task<string> ProcessCommandAsync(string messageJson, Newtonsoft.Json.Linq.JObject jObject, string clientId)
    {
        using var scope = _serviceProvider.CreateScope();

        var commandType = jObject["CommandType"]?.ToString();

        if (string.IsNullOrEmpty(commandType))
        {
            return CreateErrorResponse("CommandType field is missing or empty");
        }
        if (_channelSecurity?.IsCommandAuthorized(commandType) != true)
        {
            _logger.LogWarning("Client {ClientId} is not authorized for command {CommandType}", clientId, commandType);
            return CreateErrorResponse($"Command is not authorized: {commandType}");
        }

        _logger.LogInformation("Handling command: {CommandType}", commandType);

        return await _registry.DispatchAsync(commandType, messageJson, scope);
    }
}
