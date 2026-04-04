using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common.Infrastructure.Tasks
{
    public interface ILongRunningTaskClient
    {
        void OnProgress(int progress, object args);
        void OnError(Exception error);
        void OnSuccess(object result);
        bool IsCancellationRequested { get; set; }
    }
}
