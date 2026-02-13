using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class NumericScore : Score
    {
        public NumericScore(float value, float Confidence, bool isValid)
            : base(Confidence, Confidence)
        {
            Value = value;
        }

       
        public float Value { get; set; }
        
        public override ValueType ValueType { get { return ValueType.Float; }  }

    }
}
