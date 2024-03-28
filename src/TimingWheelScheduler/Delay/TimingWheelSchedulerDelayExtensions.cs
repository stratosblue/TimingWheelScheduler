using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks;

/// <summary>
///
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TimingWheelSchedulerDelayExtensions
{
    #region Private 字段

    private static readonly ScheduledTask s_completedScheduledTask;

    #endregion Private 字段

    #region Public 构造函数

    /// <inheritdoc cref="TimingWheelSchedulerDelayExtensions"/>
    static TimingWheelSchedulerDelayExtensions()
    {
        s_completedScheduledTask = new(0, static () => { }, null!, TaskScheduler.Default)
        {
            _status = ScheduledTask.Status_Executed
        };
    }

    #endregion Public 构造函数

    #region Public 方法

    /// <inheritdoc cref="Delay(TimingWheelScheduler, TimeSpan, CancellationToken)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimingWheelSchedulerDelayAwaiter Delay(this TimingWheelScheduler scheduler, TimeSpan delay)
    {
        scheduler.CheckDelayTimeSpan(delay);

        var scheduledTask = scheduler.InternalSchedule(delay, static () => { }, default);

        return new TimingWheelSchedulerDelayAwaiter(scheduledTask ?? s_completedScheduledTask, default);
    }

    /// <summary>
    /// 使用 <paramref name="scheduler"/> 进行时间长度为 <paramref name="delay"/> 的延时
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="delay">延时时长</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimingWheelSchedulerDelayAwaiter Delay(this TimingWheelScheduler scheduler, TimeSpan delay, CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
               ? InternalDelayWithCancellationToken(scheduler, delay, cancellationToken)
               : Delay(scheduler, delay);
    }

    #endregion Public 方法

    #region Private 方法

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimingWheelSchedulerDelayAwaiter InternalDelayWithCancellationToken(this TimingWheelScheduler scheduler, TimeSpan delay, CancellationToken cancellationToken)
    {
        scheduler.CheckDelayTimeSpan(delay);

        var callbackDisposer = new ScheduledTaskCallbackDisposer(cancellationToken);
        var scheduledTask = scheduler.InternalSchedule(delay, callbackDisposer.Dispose, default);
        if (scheduledTask is null)
        {
            return new TimingWheelSchedulerDelayAwaiter(s_completedScheduledTask, default);
        }

        callbackDisposer.SetScheduledTask(scheduledTask);

        var result = new TimingWheelSchedulerDelayAwaiter(scheduledTask, cancellationToken);

        return result;
    }

    #endregion Private 方法

    #region Private 类

    private sealed class ScheduledTaskCallbackDisposer(CancellationToken cancellationToken)
    {
        #region Private 字段

        private CancellationTokenRegistration _cancellationTokenRegistration;

        private volatile int _status = 0;

        #endregion Private 字段

        #region Public 方法

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _status, 1, 0) == 0)
            {
                _cancellationTokenRegistration.Dispose();
                _cancellationTokenRegistration = default;
            }
        }

        public void SetScheduledTask(ScheduledTask scheduledTask)
        {
            if (_status == 0)
            {
                _cancellationTokenRegistration = cancellationToken.Register(static state =>
                {
                    using var task = (ScheduledTask)state!;
                    task.Execute();
                }, scheduledTask, false);
                if (_status != 0)
                {
                    scheduledTask.Dispose();
                    _cancellationTokenRegistration.Dispose();
                }
            }
            else
            {
                scheduledTask.Dispose();
            }
        }

        #endregion Public 方法
    }

    #endregion Private 类
}
