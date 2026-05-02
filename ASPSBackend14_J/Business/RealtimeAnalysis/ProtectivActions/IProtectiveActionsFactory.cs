using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using Common.Interfaces;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.ProtectivActions
{
    public interface IProtectiveActionsFactory
    {
        //IProtectiveAction[] CreateProtectiveActions(AnalysisResult analysisResult);
        IProtectiveAction[] CreateProtectiveActions(AnalysisResult analysisResult, AnalyzerResult analyzerResult, string alertId, DeviceInfo deviceInfo, float trackUrlThreshold);
    }
}
