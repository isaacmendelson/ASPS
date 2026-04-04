#nullable enable

using Common.Infrastructure.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Infrastructure.Tasks;

public static class LongRunningTask
{
    public static Task Run(Action action)
    {
        return Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
    }

    public static Task Run(Action action, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public static Task Run(Action<CancellationToken> action, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() => action(cancellationToken), cancellationToken, 
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public static Task Run(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(async() => await action(cancellationToken), cancellationToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
}
