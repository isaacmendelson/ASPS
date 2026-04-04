#nullable enable

using Common;
using Common.Infrastructure;

namespace Common.Infrastructure.Tasks;

public interface IBackgroundTask
{
    void Start();
    void Stop();
}
