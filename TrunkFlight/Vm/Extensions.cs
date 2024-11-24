using System;
using System.Threading;
using System.Threading.Tasks;
using R3;

namespace TrunkFlight.Vm;

public static class Extensions
{
    public static IDisposable SubscribeExclusiveAwait<T>(
        this ReactiveCommand<T> command,
        Func<T, CancellationToken, ValueTask> onNextAsync
    )
    {
        return command.SubscribeAwait((t, ct) =>
        {
            command.ChangeCanExecute(false);
            var task = onNextAsync.Invoke(t, ct);
            task.ConfigureAwait(true)
                .GetAwaiter()
                .OnCompleted(() => command.ChangeCanExecute(true));
            return task;
        }, AwaitOperation.Drop, maxConcurrent: 1);
    }
}
