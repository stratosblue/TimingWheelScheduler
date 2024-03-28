namespace System.Threading.Tasks;

/// <summary>
/// 无操作 <see cref="IDisposable"/>
/// </summary>
internal sealed class NoopDisposer : IDisposable
{
    #region Public 属性

    /// <summary>
    /// 共享实例
    /// </summary>
    public static NoopDisposer Shared { get; } = new();

    #endregion Public 属性

    #region Public 方法

    /// <inheritdoc/>
    public void Dispose()
    { }

    #endregion Public 方法
}
