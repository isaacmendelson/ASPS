using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class KnownWebsiteTemplateIndicator : Indicator
    {
        protected KnownWebsiteTemplateIndicator() { }

        protected KnownWebsiteTemplateIndicator(NumericScore score, AnalysisLevel layer, int sequence, float weight)
            : base(score, layer, sequence, weight)
        {
        }

        public override IndicatorType IndicatorType { get => IndicatorType.ContentKnownWebsiteTemplate; }
        public BooleanScore Score { get; private set; }

        public override IndicatorSource Source { get => IndicatorSource.Content; }
        public void SetScore(BooleanScore score)
        {
            this.Score = score;
        }

        public bool TypedValue { get; private set; }
        public void SetValue(bool value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
