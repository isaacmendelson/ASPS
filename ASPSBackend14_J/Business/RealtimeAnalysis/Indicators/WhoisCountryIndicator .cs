#nullable enable

using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Business.RealtimeAnalysis.Indicators
{
    public class WhoisCountryIndicator : Indicator
    {
        protected WhoisCountryIndicator() { }

        public WhoisCountryIndicator(NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
        }

        public WhoisCountryIndicator(string value, NumericScore score, AnalysisLevel layer, int? sequence, float? weight = 1)
            : base(score, layer, sequence ?? 0, weight ?? 1)
        {
            this.Value = value;
        }

        public override IndicatorType IndicatorType { get => IndicatorType.WhoIsCountry; }
        public override IndicatorSource Source { get => IndicatorSource.Domain; }

        public NumericScore? Score { get; set; }
       // public override object? Value { get; private set; }
        public string TypedValue { get; private set; }

        
        public void SetScore(NumericScore score)
        {
            this.Score = score;
        }

        public void SetValue(string value, float confidence)
        {
            base.SetValue(value, confidence);
            this.TypedValue = value;
        }
    }
}
