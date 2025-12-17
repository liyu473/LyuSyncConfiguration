namespace LyuSyncConfiguration.Abstractions;

/// <summary>
/// 同步配置接口，定义配置同步的基本契约
/// </summary>
/// <typeparam name="T">配置类型</typeparam>
public interface ISyncConfiguration<T> : IDisposable where T : class, new()
{
    /// <summary>
    /// 当前配置值
    /// </summary>
    T Value { get; }

    /// <summary>
    /// 配置文件路径
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// 配置变更事件
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;

    /// <summary>
    /// 保存当前配置到文件
    /// </summary>
    void Save();

    /// <summary>
    /// 从文件重新加载配置
    /// </summary>
    void Reload();

    /// <summary>
    /// 更新配置并保存到文件（支持防抖）
    /// </summary>
    /// <param name="updateAction">更新操作</param>
    void Update(Action<T> updateAction);

    /// <summary>
    /// 批量更新配置，多次调用会自动合并保存
    /// </summary>
    /// <param name="updateAction">更新操作</param>
    void BatchUpdate(Action<T> updateAction);
}
