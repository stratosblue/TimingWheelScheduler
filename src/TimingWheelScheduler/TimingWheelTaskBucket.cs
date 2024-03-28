using System.Collections;
using System.Diagnostics;

namespace System.Threading.Tasks;

/// <summary>
/// 时间轮任务桶
/// </summary>
/// <param name="taskNodeSize"></param>
[DebuggerDisplay("TaskCount: {_taskCount,nq}")]
internal sealed class TimingWheelTaskBucket(int taskNodeSize) : IEnumerable<ScheduledTask>
{
    //TODO 优化内存使用

    #region Private 字段

    private readonly LinkedList<ScheduledTask[]> _scheduledTasks = new();

    private ScheduledTask[]? _currentNode;

    private int _currentNodeIndex;

    private uint _taskCount;

    #endregion Private 字段

    #region Public 属性

    public uint TaskCount => _taskCount;

    #endregion Public 属性

    #region Public 方法

    public void Append(ScheduledTask scheduledTask)
    {
        if (_currentNode is null)
        {
            _currentNode = new ScheduledTask[taskNodeSize];
            _scheduledTasks.AddLast(_currentNode);
        }

        _currentNode[_currentNodeIndex++] = scheduledTask;

        if (_currentNodeIndex == taskNodeSize)
        {
            _currentNodeIndex = 0;
            _currentNode = null;
        }

        _taskCount++;
    }

    /// <summary>
    /// 处理当前 <see cref="TimingWheelTaskBucket"/> 的所有计划任务（执行或者进行时间轮调度）
    /// </summary>
    public void Flush()
    {
        if (_taskCount == 0)
        {
            return;
        }
        foreach (var taskList in _scheduledTasks)
        {
            foreach (var scheduledTask in taskList)
            {
                if (scheduledTask is null)
                {
                    break;
                }
                if (scheduledTask.RemainTickCount == 0)
                {
                    scheduledTask.Execute();
                    scheduledTask.Dispose();
                }
                else
                {
                    scheduledTask.RemainTickCount--;
                    scheduledTask.TimingWheelScheduler.EnqueueScheduledTask(scheduledTask);
                }
            }
        }
        _taskCount = 0;
        _currentNodeIndex = 0;
        _scheduledTasks.Clear();

        if (_currentNode is not null)
        {
            Array.Clear(_currentNode!, 0, _currentNode!.Length);
            _scheduledTasks.AddLast(_currentNode);
        }
    }

    /// <inheritdoc/>
    IEnumerator<ScheduledTask> IEnumerable<ScheduledTask>.GetEnumerator() => new TimingWheelTaskBucketEnumerator(this);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => (this as IEnumerable<ScheduledTask>).GetEnumerator();

    #endregion Public 方法

    #region Private 类

    private sealed class TimingWheelTaskBucketEnumerator(TimingWheelTaskBucket timingWheelTaskBucket) : IEnumerator<ScheduledTask>
    {
        //TODO 测试

        #region Private 字段

        private LinkedListNode<ScheduledTask[]>? _currentNode = timingWheelTaskBucket._scheduledTasks.First;

        private int _currentNodeIndex = 0;

        #endregion Private 字段

        #region Public 属性

        public ScheduledTask Current { get; private set; } = null!;

        object IEnumerator.Current => Current;

        #endregion Public 属性

        #region Public 方法

        public void Dispose()
        { }

        public bool MoveNext()
        {
            if (timingWheelTaskBucket._taskCount == 0)
            {
                return false;
            }

            if (_currentNode is null)
            {
                return false;
            }
            if (_currentNodeIndex < _currentNode.Value.Length)
            {
                var current = _currentNode.Value[_currentNodeIndex++];
                Current = current;
                return current != null;
            }
            else
            {
                _currentNode = _currentNode.Next;
                _currentNodeIndex = 0;
                return MoveNext();
            }
        }

        public void Reset()
        {
            _currentNode = timingWheelTaskBucket._scheduledTasks.First;
            _currentNodeIndex = 0;
        }

        #endregion Public 方法
    }

    #endregion Private 类
}
