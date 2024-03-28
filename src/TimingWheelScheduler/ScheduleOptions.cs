namespace System.Threading.Tasks;

/// <summary>
/// 调度选项
/// </summary>
public record struct ScheduleOptions
{
    /// <summary>
    /// 执行时所使用的 <see cref="Tasks.TaskScheduler"/>
    /// </summary>
    public TaskScheduler? TaskScheduler { get; set; }
}
