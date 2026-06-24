namespace LyuSyncConfiguration.Abstractions;

/// <summary>
/// 同步配置接口，定义配置同步的基本契约。
/// </summary>
/// <typeparam name="T">配置类型</typeparam>
public interface ISyncConfiguration<T> : IDisposable where T : class, new()
{
    /// <summary>
    /// 当前配置值。
    /// </summary>
    T Value { get; }

    /// <summary>
    /// 配置文件路径。
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// 配置变更事件。
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;

    /// <summary>
    /// 保存当前配置到文件。
    /// </summary>
    void Save();

    /// <summary>
    /// 从文件重新加载配置。
    /// </summary>
    void Reload();

    /// <summary>
    /// 更新配置并保存到文件，支持防抖。
    /// </summary>
    /// <param name="updateAction">更新操作</param>
    void Update(Action<T> updateAction);

    /// <summary>
    /// 批量更新配置，多次调用会自动合并保存。
    /// </summary>
    /// <param name="updateAction">更新操作</param>
    void BatchUpdate(Action<T> updateAction);

    /// <summary>
    /// 按路径只更新当前配置节点中的某个片段，并立即保存到文件。
    /// 当配置设置了 SectionName 时，keyPath 相对于该节内部路径。
    /// 例如：IoConfig:0:IsEnabled。
    /// </summary>
    /// <typeparam name="TValue">片段值类型</typeparam>
    /// <param name="keyPath">冒号分隔的路径，支持数组下标</param>
    /// <param name="value">要写入的值</param>
    void UpdateFragment<TValue>(string keyPath, TValue value);
}
