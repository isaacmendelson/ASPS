using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.RealtimeAnalysis.Indicators
{
    public interface IIndicator
    {

        void SetWeight(float weight);

        void SetScore(IScore score);
    }
}
