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

    /// <summary>
    /// 创建简单值同步，用于单个配置项的读写
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径（支持冒号分隔的嵌套路径，如 "Server:Port"）</param>
    /// <param name="defaultValue">默认值</param>
    public static ISyncValue<T> CreateValue<T>(string filePath, string key, T defaultValue)
    {
        return new SyncValue<T>(filePath, key, defaultValue);
    }

    /// <summary>
    /// 使用选项创建简单值同步
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="configure">配置选项委托</param>
    public static ISyncValue<T> CreateValue<T>(string filePath, string key, T defaultValue, Action<SyncConfigurationOptions> configure)
    {
        var options = new SyncConfigurationOptions();
        configure(options);
        return new SyncValue<T>(filePath, key, defaultValue, options);
    }
}
