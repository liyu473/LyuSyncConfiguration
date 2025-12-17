using System.Text.Json;
using LyuSyncConfiguration.Abstractions;

namespace LyuSyncConfiguration.Serializers;

/// <summary>
/// 基于 System.Text.Json 的克隆序列化器
/// 优点：无需额外配置，兼容所有类型
/// 缺点：性能相对较慢
/// </summary>
public class JsonCloneSerializer : ICloneSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// 创建 JSON 克隆序列化器
    /// </summary>
    /// <param name="options">JSON 序列化选项（可选）</param>
    public JsonCloneSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc/>
    public T? Clone<T>(T value)
    {
        if (value == null) return default;
        var json = JsonSerializer.Serialize(value, _options);
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
