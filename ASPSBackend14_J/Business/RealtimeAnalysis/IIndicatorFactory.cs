using Business.RealtimeAnalysis.Indicators;
using Business.RealtimeAnalysis.UserDomain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public interface IIndicatorFactory
    {
        IIndicator[] CreateIndicators(AnalysisResult analysisResult);
    }
}
