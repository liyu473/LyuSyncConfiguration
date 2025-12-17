namespace LyuSyncConfiguration.Abstractions;

/// <summary>
/// 克隆序列化器接口
/// 用于配置对象的深克隆操作，支持不同的序列化实现
/// </summary>
public interface ICloneSerializer
{
    /// <summary>
    /// 克隆对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="value">要克隆的对象</param>
    /// <returns>克隆后的新对象</returns>
    T? Clone<T>(T value);
}

/// <summary>
/// 克隆序列化器类型枚举
/// </summary>
public enum CloneSerializerType
{
    /// <summary>
    /// 使用 System.Text.Json 进行克隆（默认，兼容性好）
    /// </summary>
    Json,
    
    /// <summary>
    /// 使用 MemoryPack 进行克隆（性能更好，需要配置类添加 [MemoryPackable] 特性）
    /// </summary>
    MemoryPack
}
