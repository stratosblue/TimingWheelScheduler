using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks;

/// <summary>
/// 基于时间轮的计划任务调度器
/// </summary>
[DebuggerDisplay("accuracy: {TickSpan/10000,nq}ms scheduled: {_scheduledTaskCount,nq} pending: {_unprocessedScheduledTasksQueue.Count,nq}")]
public sealed class TimingWheelScheduler
{
    #region declaration

    /// <summary>
    /// 最大可支持的时间轮层数
    /// </summary>
    public const int MaxAvailableNumberOfWheel = byte.MaxValue;

    /// <summary>
    /// 最大可支持的每层时间轮tick数量
    /// </summary>
    public const int MaxAvailableTicksPerWheel = ushort.MaxValue;

    /// <summary>
    /// 允许的最大tick时间长度<br/>
    /// 虽然允许的最大时间长度为 60min，但不建议使用过大的值，
    /// </summary>
    public static readonly TimeSpan MaxTickSpan = TimeSpan.FromMinutes(60);

    /// <summary>
    /// 允许的最小tick时间长度<br/>
    /// 虽然允许的最小时间长度为 1ms，但不建议使用如此小的值，受多种条件的影响 <see cref="Thread.Sleep(int)"/> 无法保证这种精度的睡眠
    /// </summary>
    public static readonly TimeSpan MinTickSpan = TimeSpan.FromMilliseconds(1);

    #endregion declaration

    #region Private 字段

    /// <inheritdoc cref="TimingWheelSchedulerOptions.NumberOfWheels"/>
    private readonly int _numberOfWheels;

    private readonly CancellationTokenSource _runningCTS;

    private readonly SemaphoreSlim _tickSemaphore;

    /// <inheritdoc cref="TimingWheelSchedulerOptions.TicksPerWheel"/>
    private readonly int _ticksPerWheel;

    /// <summary>
    /// 未处理的计划任务队列<br/>
    /// 调用 <see cref="Schedule(TimeSpan, Action, ScheduleOptions)"/> 方法时将计划任务添加入该队列<br/>
    /// 等待下一次tick执行时从该队列取出计划任务并分配入对应的时间轮
    /// </summary>
    private readonly ConcurrentQueue<ScheduledTask> _unprocessedScheduledTasksQueue = new();

    private volatile bool _disposed;

    /// <summary>
    /// 已计划的任务数量
    /// </summary>
    private int _scheduledTaskCount;

    #endregion Private 字段

    #region Internal 字段

    /// <summary>
    /// 每层轮完成当前周期的剩余tick总次数
    /// </summary>
    internal readonly long[] _nowTimingWheelsTickCounts;

    /// <summary>
    /// 所有时间轮
    /// </summary>
    internal readonly TimingWheel[] _timingWheels;

    /// <summary>
    /// 对应 <see cref="_timingWheels"/> 的每层时间轮需要的 <see cref="TickSpan"/> tick总次数
    /// </summary>
    internal readonly long[] _timingWheelsTickCounts;

    #endregion Internal 字段

    #region Internal 属性

    /// <summary>
    /// 还在等待执行的Tick数量
    /// </summary>
    internal int WaitingTicks => _tickSemaphore.CurrentCount;

    #endregion Internal 属性

    #region Public 属性

    /// <summary>
    /// 最大可计划的延迟时间Ticks，超过此值的任务无法进行处理<br/>
    /// 其值根据 <see cref="TimingWheelSchedulerOptions.TicksPerWheel"/>、<see cref="TimingWheelSchedulerOptions.NumberOfWheels"/>、<see cref="TimingWheelSchedulerOptions.TickSpan"/> 计算
    /// </summary>
    public long MaxSchedulableDelayTicks { get; }

    /// <inheritdoc cref="_scheduledTaskCount"/>
    public int ScheduledTaskCount => _scheduledTaskCount;

    /// <summary>
    /// 每次tick对应的时间ticks长度
    /// </summary>
    public long TickSpan { get; }

    #endregion Public 属性

    #region Private 构造函数

    private TimingWheelScheduler(TimingWheelSchedulerOptions options)
    {
        var (tickSpan, ticksPerWheel, numberOfWheels) = options;

        if (tickSpan < MinTickSpan
            || tickSpan > MaxTickSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.TickSpan)} value \"{tickSpan}\" not between \"{MinTickSpan}\" and \"{MaxTickSpan}\" ");
        }

        if (ticksPerWheel < 1
            || ticksPerWheel > MaxAvailableTicksPerWheel)
        {
            throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.TicksPerWheel)} value \"{ticksPerWheel}\" not between \"{1}\" and \"{MaxAvailableTicksPerWheel}\" ");
        }

        if (numberOfWheels < 1
            || numberOfWheels > MaxAvailableNumberOfWheel)
        {
            throw new ArgumentOutOfRangeException(nameof(options), $"{nameof(options.NumberOfWheels)} value \"{ticksPerWheel}\" not between \"{1}\" and \"{MaxAvailableNumberOfWheel}\" ");
        }

        TickSpan = tickSpan.Ticks;

        //最大可计划的延迟时间ticks
        //此边界值计算可能有问题
        //当前假设前置时间轮都已在最后一刻，则可用的ticks = (最后一个时间轮tick - 1) * 前置时间轮完整ticks总数
        MaxSchedulableDelayTicks = tickSpan.Ticks * Convert.ToInt64(Math.Pow(ticksPerWheel, numberOfWheels - 1)) * (ticksPerWheel);

        _timingWheels = new TimingWheel[numberOfWheels];

        for (var i = 0; i < numberOfWheels; i++)
        {
            var perTickMinimalTick = Convert.ToInt64(Math.Pow(ticksPerWheel, i));
            _timingWheels[i] = new TimingWheel(ticksPerWheel, perTickMinimalTick);
        }

        _timingWheelsTickCounts = new long[numberOfWheels];

        //计算每层时间轮的Tick总次数
        for (var i = 0; i < numberOfWheels; i++)
        {
            //边界值可能有问题
            _timingWheelsTickCounts[i] = Convert.ToInt64(Math.Pow(ticksPerWheel, i + 1));
        }

        //每层轮完成当前周期的剩余tick总次数
        _nowTimingWheelsTickCounts = new long[numberOfWheels + 1];
        Array.Copy(_timingWheelsTickCounts, 0, _nowTimingWheelsTickCounts, 1, numberOfWheels);

        _tickSemaphore = new SemaphoreSlim(0, int.MaxValue);

        _runningCTS = new CancellationTokenSource();

        _ticksPerWheel = ticksPerWheel;
        _numberOfWheels = numberOfWheels;

        //do wheel tick
        Task.Factory.StartNew(action: ThreadCallbackRunWheelTick,
                              state: this,
                              cancellationToken: CancellationToken.None,
                              creationOptions: TaskCreationOptions.LongRunning,
                              scheduler: options.TaskScheduler ?? TaskScheduler.Default);

        //tick timer task
        Task.Factory.StartNew(action: ThreadCallbackRunTick,
                              state: this,
                              cancellationToken: CancellationToken.None,
                              creationOptions: TaskCreationOptions.LongRunning,
                              scheduler: options.TaskScheduler ?? TaskScheduler.Default);
    }

    #endregion Private 构造函数

    #region Public 方法

    /// <summary>
    /// 使用 <paramref name="options"/> 创建一个指定配置的 <see cref="TimingWheelScheduler"/>
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public static ITimingWheelSchedulerManager Create(TimingWheelSchedulerOptions options)
    {
        var timingWheelScheduler = new TimingWheelScheduler(options);
        return new TimingWheelSchedulerManager(timingWheelScheduler);
    }

    /// <summary>
    /// 将委托 <paramref name="callback"/> 添加到计划执行中，延时 <paramref name="delay"/> 后执行
    /// </summary>
    /// <param name="delay">延时时长</param>
    /// <param name="callback">执行时的回调</param>
    /// <param name="options"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable Schedule(TimeSpan delay, Action callback, ScheduleOptions options = default)
    {
        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        CheckDelayTimeSpan(delay);

        return InternalSchedule(delay, callback, options) as IDisposable ?? NoopDisposer.Shared;
    }

    #endregion Public 方法

    #region Internal 方法

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckDelayTimeSpan(TimeSpan delay)
    {
        if (delay.Ticks > MaxSchedulableDelayTicks
            || delay.Ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DecrementScheduledTaskCount() => Interlocked.Decrement(ref _scheduledTaskCount);

    /// <summary>
    /// 将 <paramref name="scheduledTask"/> 添加入处理队列
    /// </summary>
    /// <param name="scheduledTask"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnqueueScheduledTask(ScheduledTask scheduledTask) => _unprocessedScheduledTasksQueue.Enqueue(scheduledTask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void IncrementScheduledTaskCount() => Interlocked.Increment(ref _scheduledTaskCount);

    /// <summary>
    /// 将委托 <paramref name="callback"/> 添加到计划执行中，延时 <paramref name="delay"/> 后执行
    /// </summary>
    /// <param name="delay">延时时长</param>
    /// <param name="callback">执行时的回调</param>
    /// <param name="options"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ScheduledTask? InternalSchedule(TimeSpan delay, Action callback, ScheduleOptions options = default)
    {
        ThrowIfDisposed();

        var tickCount = delay.Ticks / TickSpan;

        if (tickCount < 1)  //如果不需要tick，直接执行
        {
            callback.Invoke();
            return null;
        }

        var scheduledTask = new ScheduledTask(tickCount, callback, this, options.TaskScheduler);

        IncrementScheduledTaskCount();

        EnqueueScheduledTask(scheduledTask);

        return scheduledTask;
    }

    #endregion Internal 方法

    #region tick work

    /// <summary>
    /// 选择剩余tick为 <paramref name="taskRemainTickCount"/> 的任务应当放入 <paramref name="timingWheelsTickCounts"/> 对应的哪个时间轮中
    /// </summary>
    /// <param name="taskRemainTickCount"></param>
    /// <param name="timingWheelsTickCounts"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int SelectTimingWheel(in long taskRemainTickCount, in long[] timingWheelsTickCounts)
    {
        //计算应该添加到哪一层时间轮
        for (var wheelIndex = 0; wheelIndex < timingWheelsTickCounts.Length; wheelIndex++)
        {
            if (taskRemainTickCount <= timingWheelsTickCounts[wheelIndex])
            {
                return wheelIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// 线程回调执行 <see cref="RunTick"/> 方法的方法
    /// </summary>
    /// <param name="state"></param>
    internal static void ThreadCallbackRunTick(object? state) => ((TimingWheelScheduler)state!).RunTick();

    /// <summary>
    /// 线程回调执行 <see cref="RunWheelTick"/> 方法的方法
    /// </summary>
    /// <param name="state"></param>
    internal static void ThreadCallbackRunWheelTick(object? state) => ((TimingWheelScheduler)state!).RunWheelTick();

    /// <summary>
    /// 计算剩余tick为 <paramref name="taskRemainTickCount"/> 的任务应当在指定时间轮 <paramref name="wheelIndex"/> 的tick位置
    /// </summary>
    /// <param name="wheelIndex"></param>
    /// <param name="perTickMinimalTick">时间轮 <paramref name="wheelIndex"/> 的tick代表多少次最小时间轮的tick次数</param>
    /// <param name="taskRemainTickCount">任务剩余的tick量</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int CalculateTickForTargetWheel(in int wheelIndex, in long perTickMinimalTick, ref long taskRemainTickCount)
    {
        //当前任务需要的剩余tick次数 - ( 目标时间轮的上一层时间轮完成当前周期需要的剩余tick总次数 + 本次tick) = 任务被安排到指定时间刻度触发时还需要的剩余tick次数
        taskRemainTickCount -= _nowTimingWheelsTickCounts[wheelIndex] + 1;

        var tick = Convert.ToInt32(Math.DivRem(taskRemainTickCount, perTickMinimalTick, out var remainder));
        taskRemainTickCount = remainder;

        Debug.Assert(tick >= 0, null, "error tick result {0} for wheel {1}", tick, wheelIndex);

        return tick;
    }

    /// <summary>
    /// 处理队列中的任务
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ProcessQueuedScheduledTask()
    {
        //将未处理的计划任务添加到对应的时间轮中
        while (_unprocessedScheduledTasksQueue.TryDequeue(out var scheduledTask))
        {
            //当前任务需要的剩余tick次数
            var taskRemainTickCount = scheduledTask.RemainTickCount;

            var wheelIndex = SelectTimingWheel(taskRemainTickCount, _timingWheelsTickCounts);

            //超过最大可延时，理论上应该在添加时就过滤掉了
            Debug.Assert(wheelIndex >= 0 && wheelIndex < _numberOfWheels, null, "error wheel index {0}", wheelIndex);

            var targetWheel = _timingWheels[wheelIndex];

            var tick = CalculateTickForTargetWheel(wheelIndex, targetWheel.PerTickMinimalTick, ref taskRemainTickCount);
            scheduledTask.RemainTickCount = taskRemainTickCount;

            //插入到接下来到达的tick
            targetWheel.Append((targetWheel.CurrentTick + tick) % _ticksPerWheel, scheduledTask);
        }
    }

    /// <summary>
    /// 推进 <paramref name="tickCount"/> 次tick
    /// </summary>
    /// <param name="tickCount"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Tick(in int tickCount) => _tickSemaphore.Release(releaseCount: tickCount);

    /// <summary>
    /// 时间轮tick一次
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WheelTick()
    {
        //有新加入的计划任务
        if (!_unprocessedScheduledTasksQueue.IsEmpty)
        {
            ProcessQueuedScheduledTask();
        }

        //计算每层轮完成当前周期的剩余tick总次数
        for (var i = 1; i < _nowTimingWheelsTickCounts.Length; i++)
        {
            ref var timingWheelsTickCount = ref _nowTimingWheelsTickCounts[i];
            timingWheelsTickCount--;
            if (timingWheelsTickCount < 0)
            {
                timingWheelsTickCount = _timingWheelsTickCounts[i - 1];
            }
        }

        //推进时间轮
        foreach (var timingWheel in _timingWheels)
        {
            if (timingWheel.Tick())   //普通Tick则不进入下一时间轮
            {
                break;
            }
        }
    }

    /// <summary>
    /// tick计算，推进时间轮
    /// </summary>
    private void RunTick()
    {
        var cancellationToken = _runningCTS.Token;

        var startTicks = DateTime.UtcNow.Ticks - TickSpan;
        var totalTickCount = 0L;

        do
        {
            //(当前时间 - 起始时间) / tick间隔 = 当前应当进行总共多少次tick
            //当前应当进行总共多少次tick - 已进行的tick次数 = 现在应当进行多少次tick 即 releaseCount
            //Debug模式下暂停程序会导致一次性进行非常多次tick

            var nowTicks = DateTime.UtcNow.Ticks;
            var releaseCount = Convert.ToInt32((nowTicks - startTicks) / TickSpan - totalTickCount);

            if (releaseCount > 0)
            {
                try
                {
                    Tick(releaseCount);
                }
                catch (ObjectDisposedException)
                {
                    //如果已不再运行，则退出循环，否则抛出异常
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    throw;
                }
                totalTickCount += releaseCount;
            }

            //总tick数 * tick间隔 + 起始时间 + tick间隔 = 下次tick应当发生的时间
            //下次tick应当发生的时间 - 当前时间 = 应等待的时间
            var waitTicks = startTicks + totalTickCount * TickSpan + TickSpan - nowTicks;

            if (waitTicks > 0)
            {
                //Debug.WriteLine("Tick wait: {0} at {1}", waitTicks, nowTicks);

                Thread.Sleep((int)(waitTicks / TimeSpan.TicksPerMillisecond));
            }
        }
        while (!cancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 执行时间轮的tick逻辑
    /// </summary>
    private void RunWheelTick()
    {
        //使用单独的线程执行此方法，内部相关逻辑不考虑并行问题
        var cancellationToken = _runningCTS.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _tickSemaphore.Wait();
            }
            catch (ObjectDisposedException)
            {
                //如果已不再运行，则退出循环，否则抛出异常
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                throw;
            }

            //Debug.WriteLine("Do tick at {0}", DateTime.UtcNow.Ticks);

            WheelTick();
        }
    }

    #endregion tick work

    #region dispose

    /// <summary>
    /// 处理当前 <see cref="TimingWheelScheduler"/><br/>
    /// （该方法并非实现自 <see cref="IDisposable"/>，避免外部非常预期调用）<br/>
    /// 正常情况下当前对象会被 <see cref="Task"/> 持有，而永远不会被析构
    /// </summary>
    private void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            _runningCTS.Cancel();
        }
        catch { }

        _runningCTS.Dispose();

        _tickSemaphore.Dispose();

        if (_scheduledTaskCount > 0
            || !_unprocessedScheduledTasksQueue.IsEmpty)
        {
            throw new TaskCanceledException($"When disposing the scheduler, there is still \"{_scheduledTaskCount + _unprocessedScheduledTasksQueue.Count}\" scheduled task waiting to run.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimingWheelScheduler));
        }
    }

    #endregion dispose

    #region shared instance

    /// <summary>
    /// 默认共享实例<br/>
    /// <see cref="TimingWheelSchedulerOptions.TickSpan"/> = 50ms<br/>
    /// <see cref="TimingWheelSchedulerOptions.TicksPerWheel"/> = <see cref="byte.MaxValue"/><br/>
    /// <see cref="TimingWheelSchedulerOptions.NumberOfWheels"/> = 4<br/>
    /// </summary>
    public static TimingWheelScheduler Shared => TimingWheelSchedulerLazySharedInstanceClass.Instance;

    internal static class TimingWheelSchedulerLazySharedInstanceClass
    {
        #region Public 字段

        public static readonly TimingWheelScheduler Instance;

        public static readonly TimingWheelSchedulerOptions Options;

        #endregion Public 字段

        #region Public 构造函数

        static TimingWheelSchedulerLazySharedInstanceClass()
        {
            Debug.WriteLine("Creating shared TimingWheelScheduler instance");
            var options = new TimingWheelSchedulerOptions(TimeSpan.FromMilliseconds(50), byte.MaxValue, 4);
            var manager = Create(options);
            Options = options;
            Instance = manager.Scheduler;
        }

        #endregion Public 构造函数
    }

    #endregion shared instance

    #region manager

    internal sealed class TimingWheelSchedulerManager(TimingWheelScheduler scheduler) : ITimingWheelSchedulerManager
    {
        #region Public 属性

        public TimingWheelScheduler Scheduler { get; } = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        #endregion Public 属性

        #region Public 方法

        public void Dispose() => Scheduler.Dispose();

        #endregion Public 方法
    }

    #endregion manager
}
