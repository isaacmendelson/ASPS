using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis
{
    public class TextualScore : Score
    {
        public TextualScore(string value, float certainty, bool isValid)
            : base(certainty, certainty, isValid)
        {
            Value = value;
        }


        public string Value { get; set; }

        public override ValueType ValueType { get { return ValueType.Textual; } }
    }
}

