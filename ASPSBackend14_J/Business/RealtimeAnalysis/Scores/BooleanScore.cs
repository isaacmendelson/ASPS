using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class BooleanScore : Score
    {
        public BooleanScore(bool value, float certainty, bool isValid)
            : base(certainty, certainty)
        {
            Value = value;
        }


        public bool Value { get; set; }

        public override ValueType ValueType { get { return ValueType.Boolean; } }
    }
}

