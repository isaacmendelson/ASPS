using System.Collections.Generic;

namespace Common.Infrastructure.Tasks;

public interface IFeatureTask
{
    string Name
    {
        get { return GetType().Name; }
    }

    bool IsRunning { get; }

    bool Start(bool ignoreConfiguration);

    bool Stop();
}
