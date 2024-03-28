using System.Diagnostics;

namespace TimingWheelSchedulerTest;

public abstract class TimingWheelSchedulerTestBase
{
    #region Protected 字段

    protected int CallbackCount;

    protected TimingWheelSchedulerOptions Options;

    protected TimingWheelScheduler Scheduler = null!;

    protected ITimingWheelSchedulerManager SchedulerManager = null!;

    #endregion Protected 字段

    #region Public 方法

    [TestCleanup]
    public virtual void TestCleanup()
    {
        CallbackCount = 0;
        Scheduler = null!;

        try
        {
            SchedulerManager.Dispose();
        }
        catch (TaskCanceledException) { }   //忽略未执行任务的异常

        SchedulerManager = null!;
        Options = default;
    }

    [TestInitialize]
    public virtual void TestInitialize()
    {
        var options = CreateTimingWheelSchedulerOptions();
        SchedulerManager = TimingWheelScheduler.Create(options);
        Scheduler = SchedulerManager.Scheduler;
        Options = options;
        AssertCallbackCount(0);
    }

    #endregion Public 方法

    #region Protected 方法

    /// <summary>
    /// 等待20毫秒（调度器触发任务是异步的）
    /// </summary>
    /// <returns></returns>
    public static Task Delay()
    {
        return Task.Delay(TimeSpan.FromMilliseconds(20));
    }

    [DebuggerStepThrough]
    protected virtual void AssertCallbackCount(int expectedCount)
    {
        Assert.AreEqual(expectedCount, CallbackCount);
    }

    [DebuggerStepThrough]
    protected virtual int CalculateTickCount(TimeSpan delay, TimeSpan tickSpan)
    {
        var ticks = Convert.ToInt32(delay.Ticks / tickSpan.Ticks);
        return ticks > 0 ? ticks : 1;
    }

    [DebuggerStepThrough]
    protected int CalculateTickCount(TimeSpan delay) => CalculateTickCount(delay, Options.TickSpan);

    [DebuggerStepThrough]
    protected virtual async Task CheckAllTickProcessOverAsync(TimeSpan? finishDelay = null, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (Scheduler.WaitingTicks == 0)
            {
                break;
            }

            await Task.Delay(50, cancellationToken);
        }

        if (finishDelay is null)
        {
            //等待100ms，以期待callback能够执行完毕（但无法保证）
            await Task.Delay(100, cancellationToken);
        }
        else
        {
            await Task.Delay(finishDelay.Value, cancellationToken);
        }
    }

    protected abstract TimingWheelSchedulerOptions CreateTimingWheelSchedulerOptions();

    [DebuggerStepThrough]
    protected virtual void IncreaseCallback()
    {
        Interlocked.Increment(ref CallbackCount);
    }

    protected TimeSpan MultiplicationTimeSpan(TimeSpan timeSpan, int multiple)
    {
        return TimeSpan.FromTicks(timeSpan.Ticks * multiple);
    }

    [DebuggerStepThrough]
    protected IDisposable ScheduleIncreaseCallback(TimeSpan delay) => ScheduleIncreaseCallback(delay, default);

    protected virtual IDisposable ScheduleIncreaseCallback(TimeSpan delay, CancellationToken cancellationToken) => Scheduler.Schedule(delay, IncreaseCallback);

    [DebuggerStepThrough]
    protected void Tick() => Tick(1);

    protected virtual void Tick(int tickCount)
    {
        for (int i = 0; i < tickCount; i++)
        {
            Scheduler.WheelTick();
        }
    }

    protected virtual void Tick(TimeSpan delay) => Tick(CalculateTickCount(delay));

    #endregion Protected 方法
}
