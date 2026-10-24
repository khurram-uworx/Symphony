using NUnit.Framework;
using Symphony.Processing;

namespace Symphony.Tests.Processing;

[TestFixture, Category(NUnitConstants.TestCatory.Unit)]
class JobQueueTests
{
    [Test]
    public async Task NotifyNewJob_WakesWaiter()
    {
        var waiter = JobQueue.WaitForSignalAsync();
        JobQueue.NotifyNewJob();
        Assert.That(await waiter, Is.True);
    }

    [Test]
    public async Task SignalInstanceAsync_WakesWaiterOnSameInstance()
    {
        var queue = new JobQueue();
        var waiter = queue.WaitForSignalInstanceAsync();
        await (queue.SignalInstanceAsync() ?? Task.CompletedTask);
        Assert.That(await waiter, Is.True);
    }

    [Test]
    public void WaitForSignalAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await JobQueue.WaitForSignalAsync(cancellation.Token));
    }

    [Test]
    public async Task BurstNotifications_DoNotBlock()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waiter = JobQueue.WaitForSignalAsync(cancellation.Token);

        for (var i = 0; i < 100; i++)
            JobQueue.NotifyNewJob();

        Assert.That(await waiter, Is.True);
    }

    [Test]
    public void SignalInstanceAsync_DoesNotThrow()
    {
        var queue = new JobQueue();
        Assert.DoesNotThrowAsync(async () => await queue.SignalInstanceAsync());
    }
}
