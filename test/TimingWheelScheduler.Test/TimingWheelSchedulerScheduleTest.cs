namespace TimingWheelSchedulerTest;

[TestClass]
public class TimingWheelSchedulerScheduleTest
{
    #region Public 方法

    [TestMethod]
    [DataRow(50, 10, 1)]
    [DataRow(50, 20, 1)]
    [DataRow(50, 40, 1)]
    [DataRow(50, 80, 1)]
    [DataRow(50, 160, 1)]
    [DataRow(50, byte.MaxValue, 1)]
    [DataRow(50, 1_000, 1)]
    public void ShouldSingleWheelSuccessWithFullSchedule(int TickSpan, int TicksPerWheel, int NumberOfWheels)
    {
        var options = new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(TickSpan), TicksPerWheel, NumberOfWheels)
        {
            TaskScheduler = NoTickTestTaskScheduler.Shared
        };

        using var schedulerManager = TimingWheelScheduler.Create(options);
        var scheduler = schedulerManager.Scheduler;

        var totalTick = Math.Pow(TicksPerWheel, NumberOfWheels);

        var indexOfWheel = 0;

        for (var i = 0; i < totalTick; i++)
        {
            var timingWheel = scheduler._timingWheels[indexOfWheel];

            for (var indexOfTick = 0; indexOfTick < TicksPerWheel; indexOfTick++)
            {
                var taskRemainTickCount = indexOfTick + 1L;

                var targetTick = scheduler.CalculateTickForTargetWheel(indexOfWheel, timingWheel.PerTickMinimalTick, ref taskRemainTickCount);

                targetTick %= TicksPerWheel;

                Assert.AreEqual(indexOfTick, targetTick, $"indexOfWheel: {indexOfWheel} indexOfTick: {indexOfTick} TicksPerWheel: {TicksPerWheel} NumberOfWheels: {NumberOfWheels}");
            }

            scheduler.WheelTick();
        }
    }

    [TestMethod]
    [DataRow(50, 10, 2)]
    [DataRow(50, 20, 2)]
    [DataRow(50, 10, 3)]
    [DataRow(50, 20, 3)]
    [DataRow(50, 5, 4)]
    [DataRow(50, 5, 5)]
    [DataRow(50, 5, 6)]
    [DataRow(50, 5, 7)]
    public void ShouldSuccessWithFullSchedule(int TickSpan, int TicksPerWheel, int NumberOfWheels)
    {
        var options = new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(TickSpan), TicksPerWheel, NumberOfWheels)
        {
            TaskScheduler = NoTickTestTaskScheduler.Shared
        };

        using var schedulerManager = TimingWheelScheduler.Create(options);
        var scheduler = schedulerManager.Scheduler;

        var totalTick = Math.Pow(TicksPerWheel, NumberOfWheels);

        var timingWheelsTickCounts = new long[NumberOfWheels + 1];
        Array.Copy(scheduler._timingWheelsTickCounts, 0, timingWheelsTickCounts, 1, NumberOfWheels);

        for (var i = 0; i < totalTick; i++)
        {
            //从第二层开始
            for (var indexOfWheel = 1; indexOfWheel < NumberOfWheels; indexOfWheel++)
            {
                var timingWheel = scheduler._timingWheels[indexOfWheel];

                var preWheelTickCount = scheduler._timingWheelsTickCounts[indexOfWheel - 1];
                var preWheelNowTickCount = scheduler._nowTimingWheelsTickCounts[indexOfWheel];
                var offset = timingWheelsTickCounts[indexOfWheel] - preWheelNowTickCount;

                for (var indexOfTick = 0; indexOfTick < TicksPerWheel; indexOfTick++)
                {
                    {
                        var taskRemainTickCount = timingWheelsTickCounts[indexOfWheel] * (indexOfTick + 1) + 1 - offset;

                        var targetTick = scheduler.CalculateTickForTargetWheel(indexOfWheel, timingWheel.PerTickMinimalTick, ref taskRemainTickCount);

                        targetTick %= TicksPerWheel;

                        Assert.AreEqual(indexOfTick, targetTick, $"indexOfWheel: {indexOfWheel} indexOfTick: {indexOfTick} TicksPerWheel: {TicksPerWheel} NumberOfWheels: {NumberOfWheels}");
                    }

                    {    //后边界值
                        var taskRemainTickCount = timingWheelsTickCounts[indexOfWheel] * (indexOfTick + 2) + 1 - 1 - offset;

                        var targetTick = scheduler.CalculateTickForTargetWheel(indexOfWheel, timingWheel.PerTickMinimalTick, ref taskRemainTickCount);

                        targetTick %= TicksPerWheel;

                        Assert.AreEqual(indexOfTick, targetTick, $"indexOfWheel: {indexOfWheel} indexOfTick: {indexOfTick} TicksPerWheel: {TicksPerWheel} NumberOfWheels: {NumberOfWheels}");
                    }

                    {
                        //无offset
                        var originTaskRemainTickCount = timingWheelsTickCounts[indexOfWheel] * (indexOfTick + 1) + 1;
                        var taskRemainTickCount = originTaskRemainTickCount;

                        var targetTick = scheduler.CalculateTickForTargetWheel(indexOfWheel, timingWheel.PerTickMinimalTick, ref taskRemainTickCount);

                        //还原tickCount
                        var restoreTaskRemainTickCount = targetTick * timingWheel.PerTickMinimalTick + 1 + taskRemainTickCount + preWheelNowTickCount;

                        Assert.AreEqual(originTaskRemainTickCount, restoreTaskRemainTickCount);
                    }
                }
            }

            scheduler.WheelTick();
        }
    }

    #endregion Public 方法
}
