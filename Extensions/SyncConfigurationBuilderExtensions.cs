using Microsoft.Extensions.Configuration;

namespace LyuSyncConfiguration.Extensions;

/// <summary>
/// IConfigurationBuilder 扩展方法 - 保持官方调用风格
/// </summary>
public static class SyncConfigurationBuilderExtensions
{
    /// <summary>
    /// 添加可同步的 JSON 配置文件
    /// </summary>
    /// <param name="builder">配置构建器</param>
    /// <param name="path">配置文件路径</param>
    /// <param name="optional">文件是否可选</param>
    /// <param name="reloadOnChange">文件变更时是否重新加载</param>
    /// <returns>配置构建器</returns>
    public static IConfigurationBuilder AddSyncJsonFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        bool reloadOnChange = true)
    {
        var fullPath = Path.GetFullPath(path);

        if (!optional && !File.Exists(fullPath))
        {
            throw new FileNotFoundException($"配置文件不存在: {fullPath}");
        }

        // 确保文件存在（如果不存在则创建空文件）
        EnsureFileExists(fullPath, optional);

        return builder.AddJsonFile(fullPath, optional, reloadOnChange);
    }

    /// <summary>
    /// 添加带环境的可同步 JSON 配置文件（自动添加主配置和环境配置）
    /// </summary>
    /// <param name="builder">配置构建器</param>
    /// <param name="basePath">基础路径（目录）</param>
    /// <param name="fileName">文件名（默认 appsettings.json）</param>
    /// <param name="environment">环境名称（如 development）</param>
    /// <param name="reloadOnChange">文件变更时是否重新加载</param>
    /// <returns>配置构建器</returns>
    public static IConfigurationBuilder AddSyncJsonFiles(
        this IConfigurationBuilder builder,
        string basePath,
        string fileName = "appsettings.json",
        string? environment = null,
        bool reloadOnChange = true)
    {
        var settingsPath = Path.Combine(basePath, fileName);
        
        // 添加主配置文件（必须存在）
        builder.AddSyncJsonFile(settingsPath, optional: false, reloadOnChange);

        // 添加环境配置文件（可选）
        if (!string.IsNullOrEmpty(environment))
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var envFileName = $"{fileNameWithoutExt}.{environment}{extension}";
            var envSettingsPath = Path.Combine(basePath, envFileName);
            
            builder.AddSyncJsonFile(envSettingsPath, optional: true, reloadOnChange);
        }

        return builder;
    }

    private static void EnsureFileExists(string path, bool optional)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path))
        {
            if (!optional)
            {
                // 主配置文件不存在时创建空 JSON
                File.WriteAllText(path, "{}");
            }
            // optional 文件不存在就不创建
        }
    }
}
