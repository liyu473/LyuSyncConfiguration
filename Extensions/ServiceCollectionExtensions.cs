using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LyuSyncConfiguration.Abstractions;
using LyuSyncConfiguration.Core;
using LyuSyncConfiguration.Options;

namespace LyuSyncConfiguration.Extensions;

/// <summary>
/// IServiceCollection 扩展方法 - 依赖注入支持
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册同步配置到依赖注入容器
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">已构建的配置</param>
    /// <param name="sectionName">配置节名称（可选）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSyncConfiguration<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null) where T : class, new()
    {
        services.AddSingleton<ISyncConfiguration<T>>(sp =>
        {
            var config = sectionName == null
                ? configuration.Get<T>() ?? new T()
                : configuration.GetSection(sectionName).Get<T>() ?? new T();

            // 获取配置文件路径
            var filePath = GetConfigurationFilePath(configuration);

            return new SyncConfiguration<T>(new SyncConfigurationOptions
            {
                FilePath = filePath,
                SectionName = sectionName,
                EnableFileWatcher = true
            });
        });

        return services;
    }

    /// <summary>
    /// 注册同步配置到依赖注入容器（使用选项配置）
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置选项委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSyncConfiguration<T>(
        this IServiceCollection services,
        Action<SyncConfigurationOptions> configure) where T : class, new()
    {
        var options = new SyncConfigurationOptions();
        configure(options);

        services.AddSingleton<ISyncConfiguration<T>>(sp => new SyncConfiguration<T>(options));

        return services;
    }

    /// <summary>
    /// 注册同步配置到依赖注入容器（简化版本，自动检测环境）
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="sectionName">配置节名称（可选）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSyncConfiguration<T>(
        this IServiceCollection services,
        string filePath,
        string? sectionName = null) where T : class, new()
    {
        services.AddSingleton<ISyncConfiguration<T>>(sp => new SyncConfiguration<T>(new SyncConfigurationOptions
        {
            FilePath = filePath,
            SectionName = sectionName,
            // AutoDetectEnvironment 默认为 true，会自动检测环境
        }));

        return services;
    }

    #region 简单值同步注入

    /// <summary>
    /// 注册简单值同步到依赖注入容器
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径（支持冒号分隔的嵌套路径，如 "Server:Port"）</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSyncValue<T>(
        this IServiceCollection services,
        string filePath,
        string key,
        T defaultValue)
    {
        services.AddSingleton<ISyncValue<T>>(sp => new SyncValue<T>(filePath, key, defaultValue));
        return services;
    }

    /// <summary>
    /// 注册简单值同步到依赖注入容器（使用选项配置）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="configure">配置选项委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSyncValue<T>(
        this IServiceCollection services,
        string filePath,
        string key,
        T defaultValue,
        Action<SyncConfigurationOptions> configure)
    {
        var options = new SyncConfigurationOptions();
        configure(options);
        services.AddSingleton<ISyncValue<T>>(sp => new SyncValue<T>(filePath, key, defaultValue, options));
        return services;
    }

    /// <summary>
    /// 注册命名的简单值同步到依赖注入容器（支持同类型多个实例）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="name">服务名称（用于区分同类型的多个实例）</param>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddKeyedSyncValue<T>(
        this IServiceCollection services,
        string name,
        string filePath,
        string key,
        T defaultValue)
    {
        services.AddKeyedSingleton<ISyncValue<T>>(name, (sp, _) => new SyncValue<T>(filePath, key, defaultValue));
        return services;
    }

    #endregion

    private static string GetConfigurationFilePath(IConfiguration configuration)
    {
        // 尝试从 IConfigurationRoot 获取文件路径
        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers)
            {
                var providerType = provider.GetType();
                var sourceProperty = providerType.GetProperty("Source");
                if (sourceProperty != null)
                {
                    var source = sourceProperty.GetValue(provider);
                    var pathProperty = source?.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        var path = pathProperty.GetValue(source) as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }
            }
        }

        return "appsettings.json";
    }
}
