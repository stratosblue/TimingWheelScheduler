using System.Diagnostics;

namespace System.Threading.Tasks;

/// <summary>
/// 时间轮
/// </summary>
[DebuggerDisplay("{_currentTick,nq}/{_tickCount,nq}")]
internal sealed class TimingWheel
{
    #region Private 字段

    private readonly int _tickCount;

    private int _currentTick;

    #endregion Private 字段

    #region Internal 属性

    internal TimingWheelTaskBucket[] Buckets { get; }

    #endregion Internal 属性

    #region Public 属性

    public int CurrentTick => _currentTick;

    /// <summary>
    /// 当前时间轮的tick代表多少次最小时间轮的tick次数
    /// </summary>
    public long PerTickMinimalTick { get; }

    #endregion Public 属性

    #region Public 构造函数

    public TimingWheel(int tickCount, long perTickMinimalTick, int taskNodeSize = 64)
    {
        _tickCount = tickCount;
        PerTickMinimalTick = perTickMinimalTick;

        Buckets = new TimingWheelTaskBucket[tickCount];

        for (var i = 0; i < Buckets.Length; i++)
        {
            Buckets[i] = new(taskNodeSize);
        }

        _currentTick = 0;
    }

    #endregion Public 构造函数

    #region Public 方法

    public void Append(int tick, ScheduledTask scheduledTask)
    {
        Debug.Assert(tick < _tickCount, null, "error tick {0} for {1}", tick, _tickCount);

        Buckets[tick].Append(scheduledTask);
    }

    /// <summary>
    /// 执行一次
    /// </summary>
    /// <returns>
    /// 是否为普通Tick<br/>
    /// <see langword="true"/> : 普通Tick<br/>
    /// <see langword="false"/> : 完整Tick（当前轮完成一次周期Tick）
    /// </returns>
    public bool Tick()
    {
        Buckets[_currentTick++].Flush();

        //确定边界值
        if (_currentTick == _tickCount)
        {
            _currentTick = 0;
            return false;
        }
        return true;
    }

    #endregion Public 方法
}
