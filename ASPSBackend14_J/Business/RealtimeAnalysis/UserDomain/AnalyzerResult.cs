#nullable enable

using Business.RealtimeAnalysis.Indicators;
using Common.Enums;
using Common.Interfaces;

namespace Business.RealtimeAnalysis.UserDomain;


// Analyzer result class
[Serializable]
public class AnalyzerResult
{
    public AnalyzerResult(Severity severity, string message, List<IIndicator>? indicators, List<IProtectiveAction>? protectiveActions, Dictionary<string, object> details)
    {
        Severity = severity;
        Message = message;
        Indicators = indicators;
        ProtectiveActions = protectiveActions;
        Details = details;
    }

    public AnalyzerResult(Severity severity, string message)
    {
        Severity = severity;
        Message = message;
        Details = new Dictionary<string, object>();
    }

    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<IIndicator>? Indicators { get; set; }

    public List<IProtectiveAction>? ProtectiveActions { get; set; }

    public Dictionary<string, object> Details { get; set; } = new();
}
