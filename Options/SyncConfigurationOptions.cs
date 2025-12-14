using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyuSyncConfiguration.Options;

/// <summary>
/// 同步配置选项
/// </summary>
public class SyncConfigurationOptions
{
    /// <summary>
    /// 配置文件路径（默认 appsettings.json）
    /// </summary>
    public string FilePath { get; set; } = "appsettings.json";

    /// <summary>
    /// 环境名称（如 development、production），会加载 appsettings.{Environment}.json
    /// </summary>
    public string? Environment { get; set; }

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
