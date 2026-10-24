using System.Threading.Channels;

namespace Symphony.Processing;

public interface IJobQueue
{
    void NotifyNewJob();

    Task SignalAsync(CancellationToken cancellationToken = default);
}

public sealed class JobQueue : IJobQueue
{
    static readonly JobQueue defaultInstance = new();

    readonly Channel<bool> channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public static void NotifyNewJob() => defaultInstance.NotifyInstance();

    public static Task SignalAsync(CancellationToken cancellationToken = default) =>
        defaultInstance.SignalInstanceAsync(cancellationToken);

    public static async Task<bool> WaitForSignalAsync(CancellationToken cancellationToken = default) =>
        await defaultInstance.WaitForSignalInstanceAsync(cancellationToken);

    public void NotifyInstance() => channel.Writer.TryWrite(true);

    void IJobQueue.NotifyNewJob() => NotifyInstance();

    public Task SignalInstanceAsync(CancellationToken cancellationToken = default)
    {
        NotifyInstance();
        return Task.CompletedTask;
    }

    Task IJobQueue.SignalAsync(CancellationToken cancellationToken = default) =>
        SignalInstanceAsync(cancellationToken);

    public async Task<bool> WaitForSignalInstanceAsync(CancellationToken cancellationToken = default) =>
        await channel.Reader.ReadAsync(cancellationToken);
}