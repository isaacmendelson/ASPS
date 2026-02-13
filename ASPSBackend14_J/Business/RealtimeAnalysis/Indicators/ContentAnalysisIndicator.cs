using Business.RealtimeAnalysis.UserDomain;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class ContentAnalysisIndicator : Indicator
    {
        public ContentAnalysisIndicator() { }
         public ContentAnalysisIndicator(ContentAnalysis value, NumericScore score, AnalysisLevel layer, 
             int? sequence, 
             float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.Value = value;
            this.TypedValue = value;
            this.Score = score;
        }

        public ContentAnalysis Value { get; set; }
        public NumericScore Score { get; set; }

        public override IndicatorType IndicatorType { get => IndicatorType.ContentAnalysis; }

        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        public void SetScore(NumericScore score)
        {
            this.Score = score;
        }

        public ContentAnalysis TypedValue { get; private set; }
        public void SetValue(ContentAnalysis value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
