using LyuSyncConfiguration.Abstractions;
using MemoryPack;

namespace LyuSyncConfiguration.Serializers;

/// <summary>
/// 基于 MemoryPack 的克隆序列化器
/// 优点：极高性能（比 JSON 快 10-50 倍）
/// 缺点：配置类需要添加 [MemoryPackable] 特性
/// 
/// 使用示例：
/// <code>
/// [MemoryPackable]
/// public partial class AppConfig
/// {
///     public string AppName { get; set; } = "MyApp";
///     public int Port { get; set; } = 8080;
/// }
/// </code>
/// </summary>
public class MemoryPackCloneSerializer : ICloneSerializer
{
    /// <inheritdoc/>
    public T? Clone<T>(T value)
    {
        if (value == null) return default;
        var bytes = MemoryPackSerializer.Serialize(value);
        return MemoryPackSerializer.Deserialize<T>(bytes);
    }
}
