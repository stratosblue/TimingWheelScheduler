using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks;

/// <summary>
/// <inheritdoc cref="TimingWheelScheduler"/> 的等待器
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct TimingWheelSchedulerDelayAwaiter : ICriticalNotifyCompletion, INotifyCompletion
{
    #region Internal 字段

    internal readonly ScheduledTask _scheduledTask;

    #endregion Internal 字段

    #region Private 字段

    private readonly CancellationToken _cancellationToken;

    #endregion Private 字段

    #region Public 属性

    /// <summary>
    /// 是否已完成
    /// </summary>
    public readonly bool IsCompleted => _scheduledTask.IsCompleted;

    #endregion Public 属性

    #region Internal 构造函数

    internal TimingWheelSchedulerDelayAwaiter(ScheduledTask scheduledTask, CancellationToken cancellationToken)
    {
        _scheduledTask = scheduledTask;
        _cancellationToken = cancellationToken;
    }

    #endregion Internal 构造函数

    #region Public 方法

    /// <summary>
    /// 获取 Awaiter
    /// </summary>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly TimingWheelSchedulerDelayAwaiter GetAwaiter() => this;

    /// <summary>
    /// 获取结果
    /// </summary>
#if NETSTANDARD2_1_OR_GREATER
    [StackTraceHidden]
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void GetResult()
    {
        if (!_scheduledTask.IsCompleted)
        {
            _scheduledTask.InternalWait();
        }
        _cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnCompleted(Action continuation)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _scheduledTask.InternalSetCallback(continuation);
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void UnsafeOnCompleted(Action continuation)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _scheduledTask.InternalSetCallback(continuation);
    }

    #endregion Public 方法
}
