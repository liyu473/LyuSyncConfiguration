namespace LyuSyncConfiguration.Abstractions;

/// <summary>
/// 简单值同步变更事件参数
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public class ValueChangedEventArgs<T> : EventArgs
{
    /// <summary>
    /// 旧值
    /// </summary>
    public T? OldValue { get; }

    /// <summary>
    /// 新值
    /// </summary>
    public T NewValue { get; }

    /// <summary>
    /// 变更来源
    /// </summary>
    public ConfigurationChangeSource Source { get; }

    public ValueChangedEventArgs(T? oldValue, T newValue, ConfigurationChangeSource source)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Source = source;
    }
}

/// <summary>
/// 简单值同步接口，用于单个配置项的读写
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public interface ISyncValue<T> : IDisposable
{
    /// <summary>
    /// 当前值
    /// </summary>
    T Value { get; }

    /// <summary>
    /// 配置键路径（如 "Server:Port"）
    /// </summary>
    string Key { get; }

    /// <summary>
    /// 配置文件路径
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// 值变更事件
    /// </summary>
    event EventHandler<ValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>
    /// 更新值并保存到文件
    /// </summary>
    /// <param name="newValue">新值</param>
    void Update(T newValue);

    /// <summary>
    /// 从文件重新加载值
    /// </summary>
    void Reload();
}
