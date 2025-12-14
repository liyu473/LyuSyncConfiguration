namespace LyuSyncConfiguration;

/// <summary>
/// 配置变更事件参数
/// </summary>
/// <typeparam name="T">配置类型</typeparam>
public class ConfigurationChangedEventArgs<T> : EventArgs where T : class, new()
{
    /// <summary>
    /// 变更前的配置
    /// </summary>
    public T? OldValue { get; }

    /// <summary>
    /// 变更后的配置
    /// </summary>
    public T NewValue { get; }

    /// <summary>
    /// 变更来源
    /// </summary>
    public ConfigurationChangeSource Source { get; }

    public ConfigurationChangedEventArgs(T? oldValue, T newValue, ConfigurationChangeSource source)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Source = source;
    }
}
