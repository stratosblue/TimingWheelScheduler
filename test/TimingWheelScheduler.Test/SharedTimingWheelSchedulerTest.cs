namespace TimingWheelSchedulerTest;

[TestClass]
public class SharedTimingWheelSchedulerTest
{
    #region Public 方法

    [TestMethod]
    public async Task ShouldCancelSuccess()
    {
        var count = 0;

        var disposable = TimingWheelScheduler.Shared.Schedule(TimeSpan.FromSeconds(0.5), () => count++);

        disposable.Dispose();

        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ShouldScheduleSuccess()
    {
        var count = 0;

        TimingWheelScheduler.Shared.Schedule(TimeSpan.FromSeconds(0.75), () => count++);

        Assert.AreEqual(0, count);

        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.AreEqual(1, count);
    }

    #endregion Public 方法
}
