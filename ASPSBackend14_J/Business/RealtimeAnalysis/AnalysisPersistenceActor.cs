using Business.DomainEvents;
using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Entities;
using Common.Interfaces;
using Common.Models.Alerts;
using Interface.Repositories;
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
    private readonly IAnalysisResultRepository _analysisResultRepository;
    private readonly ILogger<AnalysisPersistenceActor> _logger;

    public AnalysisPersistenceActor(
        IAnalysisResultRepository analysisResultRepository,
        ILogger<AnalysisPersistenceActor> logger)
    {
        _analysisResultRepository = analysisResultRepository;
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
        //AnalysisResult? vm = null;
        try
        {
            string jsonValue = string.Empty;

            switch (analysisEvent.AlertType)
            {
                case nameof(UrlAlert):
                    if (analysisEvent.AnalyzerResults.Any())
                    {
                        var res1 = analysisEvent.AnalyzerResults.FirstOrDefault(i => i.Value.Item1 is UrlAnalysisResultVm);
                        var vm1 = res1.Value.Item1 as UrlAnalysisResultVm;
                        var vm11 = new UrlAnalyzerResultVm(vm1,
                            res1.Value.Item2.Cast<Indicator>().ToArray(),
                            res1.Value.Item3.Cast<ProtectiveAction>().ToArray(),
                            res1.Key, analysisEvent.DeviceUid, analysisEvent.Timestamp, analysisEvent.Severity);
                        jsonValue = JsonConvert.SerializeObject(vm11, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.Auto,
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
                    }
                    break;
                case nameof(RemoteAccessAlert):
                    if (analysisEvent.AnalyzerResults.Any())
                    {
                        var res2 = analysisEvent.AnalyzerResults.FirstOrDefault(i => i.Value.Item1 is RemoteAccessAnalysisResultVm);
                        var vm2 = res2.Value.Item1 as RemoteAccessAnalysisResultVm;
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
                    break;
            }
            
           // Create AnalysisResultContainer entity
            var analysisResultContainer = new AnalysisResultContainer(
                 Guid.NewGuid().ToString(),
                analysisEvent.UserKeyField,
                analysisEvent.AnalyzerResults.Any() ? analysisEvent.AnalyzerResults.First().Value.Item1.GetType().Name : "",
                analysisEvent.Timestamp,
                jsonValue,
                false,
                null,
                false,
                analysisEvent.DeviceAlertKeyField
            );

            // Save to database
            await _analysisResultRepository.AddAsync(analysisResultContainer);
            
            _logger.LogInformation(
                $"[AnalysisPersistenceActor] Saved analysis result: " +
                $"Key={analysisResultContainer.Key.Value}, " +
                $"AlertType={analysisEvent.AlertType}, " +
                $"Severity={analysisEvent.Severity}, " +
                $"Alert={analysisEvent.DeviceAlertKeyField}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                $"[AnalysisPersistenceActor] Error saving analysis result for {analysisEvent.AlertType} device {analysisEvent.DeviceUid} at {analysisEvent.Timestamp}");
        }
    }
}
