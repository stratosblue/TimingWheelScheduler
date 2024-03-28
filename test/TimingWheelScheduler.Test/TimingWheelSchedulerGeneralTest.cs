namespace TimingWheelSchedulerTest;

[TestClass]
public class TimingWheelSchedulerGeneralTest : TimingWheelSchedulerTestBase
{
    #region Public 方法

    [TestMethod]
    public void ShouldSuccessWithMultipleDispose()
    {
        var disposable = ScheduleIncreaseCallback(TimeSpan.Zero);
        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();

        disposable = ScheduleIncreaseCallback(TimeSpan.FromMilliseconds(1));
        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();
    }

    [TestMethod]
    public async Task ShouldSuccessWithTickedWheel()
    {
        var tickCount = Options.TicksPerWheel * 4;
        AssertCallbackCount(0);
        for (int i = 0; i < tickCount; i++)
        {
            ScheduleIncreaseCallback(Options.TickSpan);
            Tick();
        }

        await Delay();

        AssertCallbackCount(tickCount);
    }

    [TestMethod]
    public async Task ShouldSuccessWithTickedWheelLesserTick()
    {
        var tickCount = Options.TicksPerWheel * 4;
        var tickSpan = Options.TickSpan - TimeSpan.FromTicks(1);
        AssertCallbackCount(0);
        for (int i = 0; i < tickCount; i++)
        {
            ScheduleIncreaseCallback(tickSpan);
            Tick();
        }

        await Delay();

        AssertCallbackCount(tickCount);
    }

    [TestMethod]
    public void ShouldSuccessWithZeroDelay()
    {
        ScheduleIncreaseCallback(default);
        AssertCallbackCount(1);
    }

    [TestMethod]
    public void ShouldThrowWhenDisposed()
    {
        SchedulerManager.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => Scheduler.Schedule(TimeSpan.FromTicks(1), () => { }));
    }

    [TestMethod]
    public void ShouldThrowWithArgumentError()
    {
        Assert.ThrowsException<ArgumentNullException>(() => Scheduler.Schedule(TimeSpan.Zero, null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Scheduler.Schedule(TimeSpan.FromTicks(-1), () => { }));

        var ticks = Options.TickSpan.Ticks * Convert.ToInt64(Math.Pow(Options.TicksPerWheel, Options.NumberOfWheels));
        Scheduler.Schedule(TimeSpan.FromTicks(ticks), () => { });
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Scheduler.Schedule(TimeSpan.FromTicks(ticks + 1), () => { }));
    }

    [TestMethod]
    public void ShouldThrowWithInvalidDelay()
    {
        var threshold = MultiplicationTimeSpan(Options.TickSpan, Options.TicksPerWheel);

        Test(threshold);

        for (int i = 0; i <= Options.TicksPerWheel; i++)
        {
            TestWithTick(threshold);
        }

        void TestWithTick(TimeSpan threshold)
        {
            Tick();
            Test(threshold);
        }

        void Test(TimeSpan threshold)
        {
            ScheduleIncreaseCallback(threshold);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScheduleIncreaseCallback(threshold + TimeSpan.FromTicks(1)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ScheduleIncreaseCallback(TimeSpan.FromTicks(-1)));
        }
    }

    #endregion Public 方法

    #region Protected 方法

    protected override TimingWheelSchedulerOptions CreateTimingWheelSchedulerOptions()
    {
        return new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(50), 100, 1)
        {
            TaskScheduler = NoTickTestTaskScheduler.Shared
        };
    }

    #endregion Protected 方法
}
