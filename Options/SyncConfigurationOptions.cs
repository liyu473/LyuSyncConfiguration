using System.Text.Json;
using System.Text.Json.Serialization;
using LyuSyncConfiguration.Abstractions;
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
    /// 保存防抖延迟时间（毫秒），默认 100ms
    /// 设为 0 则禁用防抖，每次 Update 立即保存
    /// </summary>
    public int SaveDebounceMs { get; set; } = 100;

    /// <summary>
    /// 克隆序列化器类型（默认 Json）
    /// - Json: 兼容性好，无需额外配置
    /// - MemoryPack: 性能更好（快10-50倍），但配置类需要添加 [MemoryPackable] 特性
    /// </summary>
    public CloneSerializerType CloneSerializer { get; set; } = CloneSerializerType.Json;

    /// <summary>
    /// 自定义克隆序列化器（优先级高于 CloneSerializer 枚举）
    /// 可以实现 ICloneSerializer 接口来自定义克隆逻辑
    /// </summary>
    public ICloneSerializer? CustomCloneSerializer { get; set; }

    /// <summary>
    /// JSON序列化选项（用于文件读写和 JSON 克隆）
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; set; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
