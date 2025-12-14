using LyuSyncConfiguration.Abstractions;
using LyuSyncConfiguration.Options;
using LyuSyncConfiguration.Core;

namespace LyuSyncConfiguration;

/// <summary>
/// 同步配置静态工厂类 - 简化的配置入口
/// </summary>
public static class SyncConfig
{
    /// <summary>
    /// 从配置文件创建同步配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="environment">环境名称（可选，如 development）</param>
    public static ISyncConfiguration<T> Create<T>(string filePath, string? environment = null) where T : class, new()
    {
        return new SyncConfiguration<T>(new SyncConfigurationOptions
        {
            FilePath = filePath,
            Environment = environment
        });
    }

    /// <summary>
    /// 使用选项创建同步配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configure">配置选项委托</param>
    public static ISyncConfiguration<T> Create<T>(Action<SyncConfigurationOptions> configure) where T : class, new()
    {
        var options = new SyncConfigurationOptions();
        configure(options);
        return new SyncConfiguration<T>(options);
    }
}
