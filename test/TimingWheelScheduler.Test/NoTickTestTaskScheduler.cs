using System.Reflection;

namespace TimingWheelSchedulerTest;

internal class NoTickTestTaskScheduler : TaskScheduler
{
    #region Private 字段

    private readonly FieldInfo _mActionFieldInfo;

    private readonly MethodInfo _threadCallbackRunWheelTickMethod;

    private readonly MethodInfo _threadCallbackTickMethod;

    #endregion Private 字段

    #region Public 属性

    public static NoTickTestTaskScheduler Shared { get; } = new();

    #endregion Public 属性

    #region Public 构造函数

    public NoTickTestTaskScheduler()
    {
        var threadCallbackTick = TimingWheelScheduler.ThreadCallbackRunTick;
        var threadCallbackRunWheelTick = TimingWheelScheduler.ThreadCallbackRunWheelTick;
        _threadCallbackTickMethod = threadCallbackTick.Method;
        _threadCallbackRunWheelTickMethod = threadCallbackRunWheelTick.Method;

        _mActionFieldInfo = typeof(Task).GetField("m_action", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new InvalidOperationException("can not get 'm_action' for Task");
    }

    #endregion Public 构造函数

    #region Protected 方法

    protected override IEnumerable<Task>? GetScheduledTasks() => throw new NotSupportedException();

    protected override void QueueTask(Task task)
    {
        var taskDelegate = (Delegate)_mActionFieldInfo.GetValue(task)!;
        var taskMethodInfo = taskDelegate.Method;
        if (taskMethodInfo == _threadCallbackTickMethod)
        {
            //取消tick执行，手动控制tick进行测试
            return;
        }
        else if (taskMethodInfo == _threadCallbackRunWheelTickMethod)
        {
            //取消tick执行，手动控制tick进行测试
            return;
        }

        throw new NotSupportedException("Incorrect use of the scheduler");
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false;
    }

    #endregion Protected 方法
}
