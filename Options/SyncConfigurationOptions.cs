using System.Text.Json;
using System.Text.Json.Serialization;
using LyuSyncConfiguration.Helpers;

namespace LyuSyncConfiguration.Options;

/// <summary>
/// 同步配置选项
/// </summary>
public class SyncConfigurationOptions
{
    private string? _environment;
    private bool _autoDetectEnvironment = true;

    /// <summary>
    /// 配置文件路径（默认 appsettings.json）
    /// </summary>
    public string FilePath { get; set; } = "appsettings.json";

    /// <summary>
    /// 是否自动检测环境（默认 true）
    /// 会从 DOTNET_ENVIRONMENT 或 ASPNETCORE_ENVIRONMENT 环境变量读取
    /// </summary>
    public bool AutoDetectEnvironment
    {
        get => _autoDetectEnvironment;
        set => _autoDetectEnvironment = value;
    }

    /// <summary>
    /// 环境名称（如 Development、Production），会加载 appsettings.{Environment}.json
    /// 如果 AutoDetectEnvironment 为 true 且未手动设置，会自动从环境变量检测
    /// </summary>
    public string? Environment
    {
        get => _environment ?? (_autoDetectEnvironment ? EnvironmentHelper.GetEnvironment() : null);
        set => _environment = value;
    }

    /// <summary>
    /// 配置节名称（为null时读取根节点）
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// 是否启用文件监控（默认 true）
    /// </summary>
    public bool EnableFileWatcher { get; set; } = true;

    /// <summary>
    /// JSON序列化选项
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; set; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
