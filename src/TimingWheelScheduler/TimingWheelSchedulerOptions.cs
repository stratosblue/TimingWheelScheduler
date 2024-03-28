namespace System.Threading.Tasks;

/// <summary>
/// <see cref="TimingWheelScheduler"/> 选项
/// </summary>
/// <param name="TickSpan">每次tick的时间长度</param>
/// <param name="TicksPerWheel">每层时间轮的tick数量（每层时间轮的大小）</param>
/// <param name="NumberOfWheels">时间轮的总层数</param>
public record struct TimingWheelSchedulerOptions(TimeSpan TickSpan, int TicksPerWheel, int NumberOfWheels)
{
    /// <summary>
    /// <see cref="TimingWheelScheduler"/> 内部创建tick任务时所使用的 <see cref="Tasks.TaskScheduler"/> ，不指定时使用 <see cref="TaskScheduler.Default"/>
    /// </summary>
    public TaskScheduler? TaskScheduler { get; set; }
}
