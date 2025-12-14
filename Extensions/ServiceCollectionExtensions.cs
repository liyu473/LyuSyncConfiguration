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
