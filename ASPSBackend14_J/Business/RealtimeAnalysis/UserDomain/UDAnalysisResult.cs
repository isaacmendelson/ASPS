#nullable enable

using Business.RealtimeAnalysis.Indicators;
using Common.Enums;
using Common.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Runtime.Serialization;

namespace Business.RealtimeAnalysis.UserDomain;

// Analysis result class
[Serializable]
[DataContract]
public class UDAnalysisResult
{
    private readonly IConfiguration _configuration;
    public UDAnalysisResult(AnalysisLevel analysis, Severity overallSeverity, Dictionary<string, Tuple<AnalysisResult, IIndicator[],
        IProtectiveAction[]>> analyzerResults, DateTime analysisTimestamp, UDUser user, IConfiguration configuration)
    {
        OverallSeverity = overallSeverity;
        AnalyzerResults = analyzerResults;
        //GeneratedFlags = generatedFlags;
        AnalysisTimestamp = analysisTimestamp;
        User = user;
        _configuration = configuration;
    }

    [DataMember]
    public Severity OverallSeverity { get; set; }
    [DataMember]
    public Dictionary<string, Tuple<AnalysisResult, IIndicator[], IProtectiveAction[]>> AnalyzerResults { get; set; } = new();
    [DataMember]
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;

    [DataMember]
    public UDUser User { get; set; }

    [DataMember]
    public AnalysisLevel AnalysisLevel { get; set; } = AnalysisLevel.Unknown;
}



