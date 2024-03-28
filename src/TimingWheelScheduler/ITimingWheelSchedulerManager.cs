namespace System.Threading.Tasks;

/// <summary>
/// <see cref="TimingWheelScheduler"/> 管理器
/// </summary>
public interface ITimingWheelSchedulerManager : IDisposable
{
    #region Public 属性

    /// <inheritdoc cref="TimingWheelScheduler"/>
    TimingWheelScheduler Scheduler { get; }

    #endregion Public 属性
}
