using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading.Tasks;

/// <summary>
/// 计划任务
/// </summary>
/// <param name="remainTickCount">执行当前计划任务还需要的tick次数</param>
/// <param name="callback">计划任务要执行的委托</param>
/// <param name="timingWheelScheduler">当前计划任务所属的 <see cref="Tasks.TimingWheelScheduler"/></param>
/// <param name="taskScheduler">执行当前计划任务所使用的 <see cref="Tasks.TaskScheduler"/></param>
[DebuggerDisplay("RemainTickCount: {RemainTickCount,nq} Status: {_status == 1,nq}")]
internal sealed class ScheduledTask(long remainTickCount,
                                    Action callback,
                                    TimingWheelScheduler timingWheelScheduler,
                                    TaskScheduler? taskScheduler)
    : IDisposable
{
    #region Status

    /// <summary>
    /// 已处理
    /// </summary>
    internal const int Status_Disposed = 2;

    /// <summary>
    /// 已执行
    /// </summary>
    internal const int Status_Executed = 1;

    /// <summary>
    /// 初始化
    /// </summary>
    internal const int Status_Init = 0;

    #endregion Status

    #region Private 字段

    /// <summary>
    /// 回调根节点，为空时则当然任务已经执行
    /// </summary>
    private ScheduledTaskCallbackNode? _callbackRootNode = new(callback);

    #endregion Private 字段

    #region Internal 字段

    internal volatile int _status = Status_Init;

    #endregion Internal 字段

    #region Public 属性

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted => _status != Status_Init;

    /// <summary>
    /// 执行当前计划任务还需要的tick次数
    /// </summary>
    public long RemainTickCount { get; internal set; } = remainTickCount;

    /// <summary>
    /// 执行当前计划任务所使用的 <see cref="Tasks.TaskScheduler"/>
    /// </summary>
    public TaskScheduler? TaskScheduler { get; private set; } = taskScheduler;

    /// <summary>
    /// 当前计划任务所属的 <see cref="Tasks.TimingWheelScheduler"/>
    /// </summary>
    public TimingWheelScheduler TimingWheelScheduler { get; } = timingWheelScheduler;

    #endregion Public 属性

    #region Private 方法

    private void WaitForExecute()
    {
        using var semaphore = new SemaphoreSlim(0, 1);

        if (Volatile.Read(ref _callbackRootNode) is { } node)
        {
            node.AppendForExecute(new(() => semaphore.Release()));
            semaphore.Wait(Timeout.Infinite);
        }
        else
        {
            return;
        }
    }

    #endregion Private 方法

    #region Internal 方法

    internal void InternalSetCallback(Action callbak)
    {
        switch (_status)
        {
            case Status_Init:
                if (Volatile.Read(ref _callbackRootNode) is { } node)
                {
                    node.AppendForExecute(new(callbak));
                }
                else
                {
                    callbak();
                }
                break;

            case Status_Disposed:
            case Status_Executed:
                callbak();
                break;

            default:
                throw new InvalidOperationException($"error status {_status} for scheduled task");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void InternalWait()
    {
        if (_status == Status_Init)
        {
            WaitForExecute();
        }
    }

    #endregion Internal 方法

    #region Public 方法

    /// <summary>
    /// 销毁当前计划任务
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _status, Status_Disposed) == Status_Disposed)
        {
            return;
        }

        Volatile.Write(ref _callbackRootNode, null);

        TimingWheelScheduler.DecrementScheduledTaskCount();

        TaskScheduler = null!;
    }

    /// <summary>
    /// 执行计划任务
    /// </summary>
    public void Execute()
    {
        var callbackRootNode = _callbackRootNode;
        var taskScheduler = TaskScheduler;

        if (Interlocked.CompareExchange(ref _status, Status_Executed, Status_Init) == Status_Init)
        {
            Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - ScheduledTask: {GetHashCode()}");

            Task.Factory.StartNew(action: static state => ((ScheduledTaskCallbackNode)state!).Execute(),
                                  callbackRootNode,
                                  cancellationToken: CancellationToken.None,
                                  creationOptions: TaskCreationOptions.None,
                                  scheduler: taskScheduler ?? TaskScheduler.Default);
        }
    }

    #endregion Public 方法

    #region Internal 类

    internal sealed class ScheduledTaskCallbackNode
    {
        #region Private 字段

        private readonly Action _callback;

        private ScheduledTaskCallbackNode? _next;

        private int _state = 0;

        #endregion Private 字段

        #region Public 构造函数

        public ScheduledTaskCallbackNode(Action callback)
        {
            _callback = callback;
        }

        #endregion Public 构造函数

        #region Private 方法

        private static void InternalAppendForExecute(ScheduledTaskCallbackNode targetNode, ScheduledTaskCallbackNode appendNode)
        {
            //TODO 确认临界状态

            while (targetNode is not null)
            {
                if (Volatile.Read(ref targetNode._state) == 0) //未执行
                {
                    if (Interlocked.CompareExchange(ref targetNode._next, appendNode, null) is null)
                    {
                        return;
                    }
                    targetNode = Volatile.Read(ref targetNode._next);
                }
                else //已执行
                {
                    appendNode.Execute();
                    break;
                }
            }
        }

        private static void InternalExecute(ScheduledTaskCallbackNode executeNode)
        {
            List<Exception>? exceptions = null;
            while (executeNode is not null)
            {
                if (Interlocked.CompareExchange(ref executeNode._state, 1, 0) == 0) //未执行
                {
                    try
                    {
                        executeNode._callback.Invoke();
                    }
                    catch (Exception ex)
                    {
                        exceptions ??= new(1);
                        exceptions.Add(ex);
                    }
                    executeNode = Interlocked.CompareExchange(ref executeNode._next, null, null)!; //执行next并将当前节点next置空
                }
                else
                {
                    break;
                }
            }

            if (exceptions is not null)
            {
                throw new AggregateException(exceptions);
            }
        }

        #endregion Private 方法

        #region Public 方法

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendForExecute(ScheduledTaskCallbackNode node) => InternalAppendForExecute(this, node);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute() => InternalExecute(this);

        #endregion Public 方法
    }

    #endregion Internal 类
}
