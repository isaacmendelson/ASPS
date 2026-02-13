using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class MlAnalysisIndicator : Indicator
    {
        public MlAnalysisIndicator() { }
        public MlAnalysisIndicator(MlAnalysis value, AnalysisLevel layer,
            int? sequence,
            float? weight = 1)
           : base(new NumericScore(value.Score, value.Confidence, true), AnalysisLevel.Device, 1, 1)
        {
            this.Value = value;
            this.TypedValue = value;
            //this.Score = score;
        }


        public MlAnalysis Value { get; set; }
        //public NumericScore Score { get; set; }

        public override IndicatorType IndicatorType { get => IndicatorType.ContentAnalysis; }

        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        public MlAnalysis TypedValue { get; private set; }
    }
}
