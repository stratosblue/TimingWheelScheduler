namespace TimingWheelSchedulerTest;

[TestClass]
public class SchedulerCreatingTest
{
    #region Public 方法

    [TestMethod]
    [DataRow(0, 1, 1)]
    [DataRow(1, 0, 1)]
    [DataRow(1, 1, 0)]
    [DataRow(60 * 60 * 1000 + 1, 1, 1)]
    [DataRow(1, TimingWheelScheduler.MaxAvailableTicksPerWheel + 1, 1)]
    [DataRow(1, 1, TimingWheelScheduler.MaxAvailableNumberOfWheel + 1)]
    public void ShouldFail(int milliseconds, int ticksPerWheel, int numberOfWheels)
    {
        var options = new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(milliseconds), ticksPerWheel, numberOfWheels)
        {
            TaskScheduler = NoTickTestTaskScheduler.Shared
        };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimingWheelScheduler.Create(options));
    }

    [TestMethod]
    [DataRow(1, 1, 1)]
    [DataRow(60 * 60 * 1000, 1, 1)]
    [DataRow(1, 2, 1)]
    [DataRow(1, 3, 1)]
    [DataRow(1, 1, 2)]
    [DataRow(1, 1, 3)]
    [DataRow(1, TimingWheelScheduler.MaxAvailableTicksPerWheel, 1)]
    [DataRow(1, 1, TimingWheelScheduler.MaxAvailableNumberOfWheel)]
    public void ShouldSuccess(int milliseconds, int ticksPerWheel, int numberOfWheels)
    {
        var options = new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(milliseconds), ticksPerWheel, numberOfWheels)
        {
            TaskScheduler = NoTickTestTaskScheduler.Shared
        };
        using var manager = TimingWheelScheduler.Create(options);
    }

    #endregion Public 方法
}
