using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Models.Alerts;
using Interface.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.Json;

namespace Business.RealtimeAnalysis;

/// <summary>
/// Listens for AnalysisResultReceived events and persists them to the database.
/// Follows Single Responsibility Principle - only handles persistence.
/// </summary>
public class AnalysisPersistenceActor : IDomainEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalysisPersistenceActor> _logger;

    public AnalysisPersistenceActor(
        IServiceProvider serviceProvider,
        ILogger<AnalysisPersistenceActor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Handle(IDomainEvent evt)
    {
        if (evt is AnalysisResultReceived analysisEvent)
        {
            await HandleAnalysisResultReceivedAsync(analysisEvent);
        }
    }

    public Type[] GetHandleableEvents()
    {
        return new[] { typeof(AnalysisResultReceived) };
    }

    private async Task HandleAnalysisResultReceivedAsync(AnalysisResultReceived analysisEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var _analysisResultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();
            var _deviceAlertRepository = scope.ServiceProvider.GetRequiredService<IDeviceAlertRepository>();

            string jsonValue = string.Empty;
            bool isFromCache = false;
            AnalysisResultContainer? analysisResultContainer = null;
            string guid = Guid.NewGuid().ToString();

            switch (analysisEvent.AlertType)
            {
                case nameof(UrlAlert):
                    if (analysisEvent.AnalyzerResults.Any())
                    {
                        var res1 = analysisEvent.AnalyzerResults.FirstOrDefault(i => i.Value.Item1 is UrlAnalysisResult);
                        var urlAnalysisResultVm = res1.Value.Item1 as UrlAnalysisResult;
                        isFromCache = urlAnalysisResultVm?.IsFromCache ?? false;
                        // SECURITY FIX ASPS-70: Added null check for FirstOrDefault result
                        if (res1.Value.Item1 == null)
                        {
                            _logger.LogWarning("UrlAnalysisResult not found in analyzer results");
                            break;
                        }
                        var vm1 = res1.Value.Item1 as UrlAnalysisResult;
                        var vm11 = new UrlAnalyzerResultVm(vm1,
                            res1.Value.Item2.Cast<Indicator>().ToArray(),
                            res1.Value.Item3.Cast<ProtectiveAction>().ToArray(),
                            res1.Key, analysisEvent.DeviceUid, analysisEvent.Timestamp, analysisEvent.Severity);
                        jsonValue = JsonConvert.SerializeObject(vm11, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.None,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
                    }
                    var urlAlertEntity = await _deviceAlertRepository.GetByKeyAsync(new Key(nameof(UrlAlert), analysisEvent.DeviceAlertKeyField)) as UrlAlertEntity;
                    
                    // Get first result safely
                    var firstUrlResult = analysisEvent.AnalyzerResults.Any() 
                        ? analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1 
                        : null;
                    
                    analysisResultContainer = new UrlAnalysisResultContainer(
                        urlAlertEntity?.Url ?? "",
                        guid,
                        analysisEvent.UserKeyField,
                        firstUrlResult?.GetType().Name ?? "Unknown",
                        analysisEvent.Timestamp,
                        jsonValue,
                        firstUrlResult?.Error is not null,
                        firstUrlResult?.Error?.Message,
                        isFromCache,
                        analysisEvent.DeviceAlertKeyField
                    );
                    break;
                case nameof(TrackUrlAlert):
                    if (analysisEvent.AnalyzerResults.Any())
                    {
                        var res2 = analysisEvent.AnalyzerResults.FirstOrDefault(i => i.Value.Item1 is TrackUrlAnalysisResult);
                        var trackUrlAnalysisResult = res2.Value.Item1 as TrackUrlAnalysisResult;
                        isFromCache = false;    // trackUrlAnalysisResult?.IsFromCache ?? false;

                        if (res2.Value.Item1 == null)
                        {
                            _logger.LogWarning("TrackUrlAnalysisResult not found in analyzer results");
                            break;
                        }
                        var vm2 = res2.Value.Item1 as TrackUrlAnalysisResult;
                        var vm22 = new TrackUrlAnalyzerResultVm(vm2,
                            res2.Value.Item2.Cast<Indicator>().ToArray(),
                            res2.Value.Item3.Cast<ProtectiveAction>().ToArray(),
                            res2.Key, analysisEvent.DeviceUid, analysisEvent.Timestamp, analysisEvent.Severity);
                        jsonValue = JsonConvert.SerializeObject(vm22, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.None,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
                    }
                    var trackUrlAlertEntity = await _deviceAlertRepository.GetByKeyAsync(new Key(nameof(TrackUrlAlert), analysisEvent.DeviceAlertKeyField)) as TrackUrlAlertEntity;
                    analysisResultContainer = new TrackUrlAnalysisResultContainer(
                         trackUrlAlertEntity?.Url ?? "",
                         trackUrlAlertEntity?.FromUrl ?? "",
                         guid,
                         analysisEvent.UserKeyField,
                         analysisEvent.AnalyzerResults.Any() ? analysisEvent.AnalyzerResults.First().Value.Item1.GetType().Name : "",
                         analysisEvent.Timestamp,
                         jsonValue,
                         analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error is not null,
                         analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error?.Message,
                         isFromCache,
                         analysisEvent.DeviceAlertKeyField
                     );
                    break;
                case nameof(RemoteAccessAlert):
                    if (analysisEvent.AnalyzerResults.Any())
                    {
                        var res2 = analysisEvent.AnalyzerResults.FirstOrDefault(i => i.Value.Item1 is RemoteAccessAnalysisResult);
                        // SECURITY FIX ASPS-70: Added null check for FirstOrDefault result
                        if (res2.Value.Item1 == null)
                        {
                            _logger.LogWarning("RemoteAccessAnalysisResult not found in analyzer results");
                            break;
                        }
                        var vm2 = res2.Value.Item1 as RemoteAccessAnalysisResult;
                        var vm22 = new RemoteAccessAnalyzerResultVm(vm2,
                            res2.Value.Item2.Cast<Indicator>().ToArray(),
                            res2.Value.Item3.Cast<ProtectiveAction>().ToArray(),
                            res2.Key, analysisEvent.DeviceUid, analysisEvent.Timestamp, analysisEvent.Severity);
                        jsonValue = JsonConvert.SerializeObject(vm22);
                        jsonValue = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            AnalyzerResults = vm22,
                            Severity = analysisEvent.Severity.ToString(),
                            Message = analysisEvent.Message,
                            Details = analysisEvent.Details,
                            Timestamp = analysisEvent.AnalysisTimestamp,
                            DeviceUid = analysisEvent.DeviceUid
                        });
                    }
                    var remoteAccessAlertEntity = await _deviceAlertRepository.GetByKeyAsync(new Key(nameof(RemoteAccessAlert), analysisEvent.DeviceAlertKeyField)) as RemoteAccessAlertEntity;
                    analysisResultContainer = new RemoteAccessAnalysisResultContainer(
                         remoteAccessAlertEntity?.RemoteAccessApp ?? 0,
                         remoteAccessAlertEntity?.SessionStatus,
                         guid,
                         analysisEvent.UserKeyField,
                         analysisEvent.AnalyzerResults.Any() ? analysisEvent.AnalyzerResults.First().Value.Item1.GetType().Name : "",
                         analysisEvent.Timestamp,
                         jsonValue,
                         analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error is not null,
                         analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error?.Message,
                         isFromCache,
                         analysisEvent.DeviceAlertKeyField
                     );
                    break;
            }
            
           // Create AnalysisResultContainer entity
            //var analysisResultContainer = new AnalysisResultContainer(
            //     Guid.NewGuid().ToString(),
            //    analysisEvent.UserKeyField,
            //    analysisEvent.AnalyzerResults.Any() ? analysisEvent.AnalyzerResults.First().Value.Item1.GetType().Name : "",
            //    analysisEvent.Timestamp,
            //    jsonValue,
            //    analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error is not null,
            //    analysisEvent.AnalyzerResults.FirstOrDefault().Value.Item1.Error?.Message,
            //    isFromCache,
            //    analysisEvent.DeviceAlertKeyField
            //);
            
            if (analysisResultContainer is not null)
            {
                // Save to database
                await _analysisResultRepository.AddAsync(analysisResultContainer);
            
                _logger.LogInformation(
                    $"[AnalysisPersistenceActor] Saved analysis result: " +
                    $"Key={analysisResultContainer.Key.Value}, " +
                    $"AlertType={analysisEvent.AlertType}, " +
                    $"Severity={analysisEvent.Severity}, " +
                    $"Alert={analysisEvent.DeviceAlertKeyField}");

                // Update the AnalysisKey at the DeviceAlert record:
                await _deviceAlertRepository.UpdateAnalysisKeyAsync(analysisEvent.DeviceAlertKeyField, analysisResultContainer.Key.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                $"[AnalysisPersistenceActor] Error saving analysis result for {analysisEvent.AlertType} device {analysisEvent.DeviceUid} at {analysisEvent.Timestamp}");
        }
    }
}
