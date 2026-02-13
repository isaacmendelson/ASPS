#nullable enable
using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public class DomainBlacklistedIndicator : Indicator
    {
        protected DomainBlacklistedIndicator() { }

        public DomainBlacklistedIndicator(BooleanScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
        : base(score, layer, sequence ?? 0, weight?? 1)
        {
         
        }

        protected DomainBlacklistedIndicator(BooleanScore score, AnalysisLevel layer, int sequence, float weight)
            : base(score, layer, sequence, weight)
        {
            this.Score = score;
        }

        public override IndicatorType IndicatorType { get => IndicatorType.DomainBlacklisted; }
        public BooleanScore? Score { get; set; }

        public override IndicatorSource Source { get => IndicatorSource.Domain; }
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
