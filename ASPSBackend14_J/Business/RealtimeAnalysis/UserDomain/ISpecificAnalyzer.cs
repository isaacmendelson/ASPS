#nullable enable

using Microsoft.Extensions.Configuration;
using DeviceAlert = Common.Models.DeviceAlert;

namespace Business.RealtimeAnalysis.UserDomain;

// Analyzer interface
public interface ISpecificAnalyzer
{
    ExternalAnalyzer[] ExternalAnalyzers { get; }
    bool CanAnalyze(DeviceAlert alert);
    Task<AnalyzerResult> AnalyzeAsync(DeviceAlert alert, List<DeviceAlert> historicalAlerts, IConfiguration configuration);
}
