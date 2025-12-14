namespace LyuSyncConfiguration;

/// <summary>
/// 配置变更来源
/// </summary>
public enum ConfigurationChangeSource
{
    /// <summary>
    /// 从文件加载
    /// </summary>
    FileLoad,

    /// <summary>
    /// 代码更新
    /// </summary>
    CodeUpdate,

    /// <summary>
    /// 文件监控变更
    /// </summary>
    FileWatch
}
