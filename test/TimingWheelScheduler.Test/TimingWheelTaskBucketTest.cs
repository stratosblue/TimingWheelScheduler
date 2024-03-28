namespace TimingWheelSchedulerTest;

[TestClass]
public class TimingWheelTaskBucketTest
{
    #region Public 方法

    [TestMethod]
    [DataRow(8, 8)]
    [DataRow(8, 9)]
    [DataRow(8, 16)]
    [DataRow(8, 17)]
    [DataRow(8, 24)]
    [DataRow(8, 25)]
    public void ShouldEnumerateSuccess(int nodeSize, int taskCount)
    {
        var taskBucket = new TimingWheelTaskBucket(nodeSize);
        using var schedulerManager = TimingWheelScheduler.Create(TimingWheelScheduler.TimingWheelSchedulerLazySharedInstanceClass.Options with { TaskScheduler = NoTickTestTaskScheduler.Default });
        var scheduler = schedulerManager.Scheduler;
        var list = new List<ScheduledTask>();

        for (int i = 0; i < taskCount; i++)
        {
            var task = new ScheduledTask(0L, () => { }, scheduler, TaskScheduler.Default);
            taskBucket.Append(task);
            list.Add(task);
        }

        Assert.AreEqual(taskCount, (int)taskBucket.TaskCount);

        CollectionAssert.AreEqual(list, taskBucket.ToArray());
    }

    [TestMethod]
    [DataRow(8, 8)]
    [DataRow(8, 9)]
    [DataRow(8, 16)]
    [DataRow(8, 17)]
    [DataRow(8, 24)]
    [DataRow(8, 25)]
    public async Task ShouldRequeueSuccess(int nodeSize, int taskCount)
    {
        var taskBucket = new TimingWheelTaskBucket(nodeSize);
        using var schedulerManager = TimingWheelScheduler.Create(TimingWheelScheduler.TimingWheelSchedulerLazySharedInstanceClass.Options with { TaskScheduler = NoTickTestTaskScheduler.Default });
        var scheduler = schedulerManager.Scheduler;
        var count = 0;

        for (int i = 0; i < taskCount; i++)
        {
            var task = new ScheduledTask(2L, () => Interlocked.Increment(ref count), scheduler, TaskScheduler.Default);
            taskBucket.Append(task);
        }

        Assert.AreEqual(taskCount, (int)taskBucket.TaskCount);

        Assert.AreEqual(0, count);

        taskBucket.Flush();

        Assert.AreEqual(0, (int)taskBucket.TaskCount);

        Assert.AreEqual(0, count);

        scheduler.WheelTick();

        //确保执行完成
        await Task.Delay(TimeSpan.FromMilliseconds(20));

        Assert.AreEqual(taskCount, count);
    }

    [TestMethod]
    public void ShouldSuccessWithEmptyBucket()
    {
        var taskBucket = new TimingWheelTaskBucket(10);
        Assert.AreEqual(0, (int)taskBucket.TaskCount);
        Assert.IsFalse(taskBucket.Any());
    }

    [TestMethod]
    [DataRow(8, 8)]
    [DataRow(8, 9)]
    [DataRow(8, 16)]
    [DataRow(8, 17)]
    [DataRow(8, 24)]
    [DataRow(8, 25)]
    public async Task ShouldSuccessWithMultipleNode(int nodeSize, int taskCount)
    {
        var taskBucket = new TimingWheelTaskBucket(nodeSize);
        using var schedulerManager = TimingWheelScheduler.Create(TimingWheelScheduler.TimingWheelSchedulerLazySharedInstanceClass.Options with { TaskScheduler = NoTickTestTaskScheduler.Default });
        var scheduler = schedulerManager.Scheduler;
        var count = 0;

        for (int i = 0; i < taskCount; i++)
        {
            var task = new ScheduledTask(0L, () => Interlocked.Increment(ref count), scheduler, TaskScheduler.Default);
            taskBucket.Append(task);
        }

        Assert.AreEqual(taskCount, (int)taskBucket.TaskCount);

        taskBucket.Flush();

        Assert.AreEqual(0, (int)taskBucket.TaskCount);

        //确保执行完成
        await Task.Delay(TimeSpan.FromMilliseconds(20));

        Assert.AreEqual(taskCount, count);
    }

    #endregion Public 方法
}
