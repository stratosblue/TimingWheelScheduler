namespace TimingWheelSchedulerTest;

[TestClass]
public class SchedulerDisposingTest : TimingWheelSchedulerTestBase
{
    #region Public 方法

    [TestMethod]
    public void ShouldDisposeSuccess()
    {
        Scheduler.Schedule(TimeSpan.Zero, () => { });
        SchedulerManager.Dispose();
    }

    [TestMethod]
    public void ShouldThrowWhenDisposedWithSchedulesTaskRemain()
    {
        Scheduler.Schedule(Options.TickSpan, () => { });
        Assert.ThrowsException<TaskCanceledException>(() => SchedulerManager.Dispose());
    }

    protected override TimingWheelSchedulerOptions CreateTimingWheelSchedulerOptions()
    {
        return TimingWheelScheduler.TimingWheelSchedulerLazySharedInstanceClass.Options;
    }

    #endregion Public 方法
}
