namespace TimingWheelSchedulerTest;

[TestClass]
public class SchedulerDelayTest : TimingWheelSchedulerTestBase
{
    #region Public 方法

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public async Task ShouldCancelSuccess(int milliseconds)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        var cancel = TimeSpan.FromMilliseconds(milliseconds / 2);

        cts.CancelAfter(cancel);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await Scheduler.Delay(delay, cancellationToken));

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在1个tick以内
        Assert.IsTrue(timeSpan >= cancel.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
        Assert.IsTrue(timeSpan <= cancel.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public async Task ShouldDelaySuccess(int milliseconds)
    {
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        await Scheduler.Delay(delay);

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在两个tick以内
        Assert.IsTrue(timeSpan >= delay.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
        Assert.IsTrue(timeSpan <= delay.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public async Task ShouldDelaySuccessWithCancellationToken(int milliseconds)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        await Scheduler.Delay(delay, cancellationToken);

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在两个tick以内
        Assert.IsTrue(timeSpan >= delay.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
        Assert.IsTrue(timeSpan <= delay.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public async Task ShouldSuccessAwaitCancelledMultiple(int milliseconds)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        var cancel = TimeSpan.FromMilliseconds(milliseconds / 2);

        cts.CancelAfter(cancel);

        var awaiter = Scheduler.Delay(delay, cancellationToken);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await awaiter);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await awaiter);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await awaiter);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await awaiter);

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在1个tick以内
        Assert.IsTrue(timeSpan >= cancel.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
        Assert.IsTrue(timeSpan <= cancel.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public void ShouldSuccessGetCancelledResultMultiple(int milliseconds)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        var cancel = TimeSpan.FromMilliseconds(milliseconds / 2);

        cts.CancelAfter(cancel);

        var awaiter = Scheduler.Delay(delay, cancellationToken);
        Assert.ThrowsException<OperationCanceledException>(() => awaiter.GetResult());
        Assert.ThrowsException<OperationCanceledException>(() => awaiter.GetResult());
        Assert.ThrowsException<OperationCanceledException>(() => awaiter.GetResult());
        Assert.ThrowsException<OperationCanceledException>(() => awaiter.GetResult());

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在1个tick以内
        Assert.IsTrue(timeSpan >= cancel.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
        Assert.IsTrue(timeSpan <= cancel.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
    }

    [TestMethod]
    public async Task ShouldSuccessWithZeroDelay()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        await Scheduler.Delay(TimeSpan.Zero);
        await Scheduler.Delay(TimeSpan.Zero, cancellationToken);
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public void ShouldSyncCancelSuccess(int milliseconds)
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);
        var cancel = TimeSpan.FromMilliseconds(milliseconds / 2);

        cts.CancelAfter(cancel);

        Assert.ThrowsException<OperationCanceledException>(() => Scheduler.Delay(delay, cancellationToken).GetResult());

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在1个tick以内
        Assert.IsTrue(timeSpan >= cancel.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
        Assert.IsTrue(timeSpan <= cancel.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 1)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public void ShouldSyncDelayMultipleSuccess(int milliseconds)
    {
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        var awaiter = Scheduler.Delay(delay);
        awaiter.GetResult();
        awaiter.GetResult();
        awaiter.GetResult();
        awaiter.GetResult();

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在两个tick以内
        Assert.IsTrue(timeSpan >= delay.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
        Assert.IsTrue(timeSpan <= delay.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(750)]
    [DataRow(1000)]
    [DataRow(1250)]
    public void ShouldSyncDelaySuccess(int milliseconds)
    {
        var start = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(milliseconds);

        Scheduler.Delay(delay).GetResult();

        var now = DateTime.UtcNow;
        var timeSpan = now - start;

        //在两个tick以内
        Assert.IsTrue(timeSpan >= delay.Add(-TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
        Assert.IsTrue(timeSpan <= delay.Add(TimeSpan.FromTicks(Options.TickSpan.Ticks * 2)));
    }

    #endregion Public 方法

    #region Protected 方法

    protected override TimingWheelSchedulerOptions CreateTimingWheelSchedulerOptions()
    {
        return TimingWheelScheduler.TimingWheelSchedulerLazySharedInstanceClass.Options;
    }

    #endregion Protected 方法
}
