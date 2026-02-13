using Common;
using Common.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public  class Indicator : IIndicator
    {
        protected Indicator() { }

        protected Indicator(Score score, AnalysisLevel level, int sequence, float weight)
        {
            this.Level = level;
            this.Sequence = sequence;
            this.Weight = weight;
            this.timestamp = DateTime.UtcNow;
            this.Score = score;

        }

        //public Type ValueType { 
        //    get {
        //        return typeof(WebsiteType); 
        //    }
        //}
        public virtual IndicatorType IndicatorType { get; set; }
        public virtual IndicatorSubject IndicatorSubject { get; set; }
        public string Name { get; set; } = string.Empty;
        public AnalysisLevel Level { get; set; }

        public virtual IndicatorSource Source { get; set; }

        public int Sequence { get; set; }
        
        [Range(0, 1)]
        public float Weight { get; private set; }

        [Range (0,1)]
        public float Confidence{ get; private set; }

        public virtual object?  Value { get; protected set; }

        public DateTime? timestamp { get; set; }

        public virtual IScore? Score {  get; private set; }
        
        public void SetScore(IScore score)
        {
            this.Score = score;
            //switch(this.Score)
            //{
            //    case NumericScore ns:
            //        this.Score = ns;
            //        break;
            //    case BooleanScore bs:
            //        this.Score = bs;
            //        break;
            //    case TextualScore ts:
            //        this.Score = ts;
            //        break;
            //}

        }

        public void SetWeight(float weight)
        {
            this.Weight = weight;
        }

        public void SetValue(object value, float confidence)
        {
            this.Value = value;
            this.Confidence = confidence;
            this.timestamp = DateTime.UtcNow;
        }
        public bool HasValue()
        {
            return this.Value != null;
        }   
    }
}
