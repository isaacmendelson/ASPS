#nullable enable

using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Business.RealtimeAnalysis;

namespace Business.RealtimeAnalysis.Indicators
{
    public class WhoisIsPrivacyProtectedIndicator : Indicator
    {
        protected WhoisIsPrivacyProtectedIndicator() { }

        public WhoisIsPrivacyProtectedIndicator(AnalysisLevel Level, BooleanScore score, int? sequence, float? weight = 1)
            : base(score, Level, sequence ?? 0, weight ?? 1)
        {
        }

        public WhoisIsPrivacyProtectedIndicator(bool value, BooleanScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.Value = value;
            this.TypedValue = value;
            //this.Score = score;
            //this.Confidence = score.Confidence;
        }

        public override IndicatorType IndicatorType { get => IndicatorType.WhoIsPrivacyProtected; }
        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        //public BooleanScore? Score { get; set; }
       // public override object? Value { get; private set; }
        public bool TypedValue { get; private set; }

        
        //public void SetScore(BooleanScore score)
        //{
        //    this.Score = score;
        //}

        public void SetValue(bool value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
