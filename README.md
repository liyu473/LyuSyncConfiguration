# LyuSyncConfiguration

配置文件与内存双向同步库。修改内存自动保存到文件，修改文件自动更新内存。

## 安装

```bash
dotnet add package LyuSyncConfiguration
```

## 两种使用方式

### 方式一：直接使用（无需依赖注入）

```csharp
// 1. 定义配置类
public class AppSettings
{
    public string AppName { get; set; } = "MyApp";
    public int Port { get; set; } = 8080;
}

// 2. 创建同步配置
using var config = SyncConfig.Create<AppSettings>("appsettings.json");

// 3. 读取
Console.WriteLine(config.Value.AppName);

// 4. 修改（自动保存到文件）
config.Update(c => c.Port = 9000);

// 5. 监听变更
config.ConfigurationChanged += (s, e) => Console.WriteLine($"配置变更: {e.Source}");
```

### 方式二：依赖注入

```csharp
// 注册服务
services.AddSyncConfiguration<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
});

// 在服务中使用
public class MyService
{
    private readonly ISyncConfiguration<AppSettings> _config;

    public MyService(ISyncConfiguration<AppSettings> config)
    {
        _config = config;
        
        var appName = _config.Value.AppName;      // 读取
        _config.Update(c => c.Port = 9000);       // 修改
    }
}
```

## 简单值同步（无需定义配置类）

适合只需要同步单个配置项的场景：

```csharp
// 直接使用
using var port = SyncConfig.CreateValue<int>("appsettings.json", "Server:Port", defaultValue: 8080);
Console.WriteLine(port.Value);  // 读取
port.Update(9000);              // 修改

// 依赖注入
services.AddSyncValue<int>("appsettings.json", "Server:Port", defaultValue: 8080);
```

## 配置选项

| 选项 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `FilePath` | string | `appsettings.json` | 配置文件路径 |
| `Environment` | string? | 自动检测 | 环境名称，会额外加载 `appsettings.{Environment}.json` |
| `SectionName` | string? | null | 配置节名称，null 表示读取整个文件 |
| `EnableFileWatcher` | bool | true | 是否监控文件变更 |
| `SaveDebounceMs` | int | 100 | 保存防抖延迟（毫秒），多次修改合并为一次写入 |
| `CloneSerializer` | enum | Json | 克隆序列化器：`Json` 或 `MemoryPack` |
| `AutoDetectEnvironment` | bool | true | 自动从环境变量检测环境 |

```csharp
services.AddSyncConfiguration<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
    options.Environment = "development";        // 会同时加载 appsettings.development.json
    options.SectionName = "AppSettings";        // 只读取 JSON 中的 "AppSettings" 节
    options.EnableFileWatcher = true;
    options.SaveDebounceMs = 100;
});
```

### SectionName 说明

```json
// appsettings.json
{
  "AppSettings": {
    "AppName": "MyApp",
    "Port": 8080
  },
  "Logging": { ... }
}
```

- `SectionName = null` → 读取整个 JSON 根节点
- `SectionName = "AppSettings"` → 只读取 `AppSettings` 节点

## 变更来源

| 来源 | 说明 |
|------|------|
| `FileLoad` | 调用 `Reload()` |
| `CodeUpdate` | 调用 `Update()` |
| `FileWatch` | 外部修改了文件 |

## 性能优化

```csharp
// 使用 MemoryPack 加速克隆（需要配置类添加 [MemoryPackable] 特性）
options.CloneSerializer = CloneSerializerType.MemoryPack;
```

## 许可证

MIT License
