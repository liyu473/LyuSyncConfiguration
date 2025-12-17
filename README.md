# LyuSyncConfiguration

基于微软配置库的配置文件与内存双向同步库。

## 功能特性

- **双向同步**：配置类修改后自动保存到文件，文件修改后自动更新内存
- **文件监控**：支持自动监听配置文件变更
- **多环境支持**：支持 `appsettings.json` + `appsettings.{environment}.json`
- **类型安全**：基于泛型的强类型配置
- **高性能**：无锁读取 + 写入防抖，适合高并发场景
- **简单值同步**：支持单个配置项的读写，无需定义配置类

## 项目结构

```
LyuSyncConfiguration/
├── Abstractions/           # 接口定义
│   ├── ISyncConfiguration.cs
│   └── ISyncValue.cs       # 简单值同步接口
├── Core/                   # 核心实现
│   ├── SyncConfiguration.cs
│   └── SyncValue.cs        # 简单值同步实现
├── Events/                 # 事件相关
│   ├── ConfigurationChangedEventArgs.cs
│   └── ConfigurationChangeSource.cs
├── Extensions/             # 扩展方法
│   ├── SyncConfigurationBuilderExtensions.cs
│   └── ServiceCollectionExtensions.cs
├── Options/                # 配置选项
│   └── SyncConfigurationOptions.cs
└── SyncConfig.cs           # 静态工厂入口
```

## 快速开始

### 1. 定义配置类

```csharp
public class AppSettings
{
    public string AppName { get; set; } = "MyApp";
    public int Port { get; set; } = 8080;
}
```

### 2. 创建同步配置

```csharp
// 方式一：简单创建
using var config = SyncConfig.Create<AppSettings>("appsettings.json");

// 方式二：带环境配置
using var config = SyncConfig.Create<AppSettings>("appsettings.json", "development");

// 方式三：使用选项配置
using var config = SyncConfig.Create<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
    options.Environment = "development";
    options.SectionName = "AppSettings";
    options.EnableFileWatcher = true;
});
```

### 3. 读取配置

```csharp
Console.WriteLine(config.Value.AppName);
Console.WriteLine(config.Value.Port);
```

### 4. 修改并保存配置

```csharp
config.Update(c => {
    c.AppName = "NewAppName";
    c.Port = 9000;
});
```

### 5. 监听配置变更

```csharp
config.ConfigurationChanged += (sender, e) => {
    Console.WriteLine($"配置已变更，来源: {e.Source}");
    Console.WriteLine($"新值: {e.NewValue.AppName}");
};
```

## 配置选项

| 选项 | 说明 | 默认值 |
|------|------|--------|
| `FilePath` | 配置文件路径 | `appsettings.json` |
| `Environment` | 环境名称 | `null` |
| `SectionName` | 配置节名称 | `null` |
| `EnableFileWatcher` | 启用文件监控 | `true` |
| `SaveDebounceMs` | 保存防抖延迟（毫秒） | `100` |
| `CloneSerializer` | 克隆序列化器类型 | `Json` |
| `CustomCloneSerializer` | 自定义克隆序列化器 | `null` |
| `JsonOptions` | JSON序列化选项 | - |

## 官方风格配置（IConfigurationBuilder）

```csharp
var appDirectory = AppContext.BaseDirectory;
var settingsPath = Path.Combine(appDirectory, "appsettings.json");
var devSettingsPath = Path.Combine(appDirectory, "appsettings.development.json");

var config = new ConfigurationBuilder()
    .AddSyncJsonFile(settingsPath, optional: false, reloadOnChange: true)
    .AddSyncJsonFile(devSettingsPath, optional: true, reloadOnChange: true)
    .Build();

// 或者使用简化方法
var config = new ConfigurationBuilder()
    .AddSyncJsonFiles(appDirectory, "appsettings.json", "development")
    .Build();
```

## 依赖注入

```csharp
// 方式一：使用配置选项
services.AddSyncConfiguration<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
    options.Environment = "development";
    options.SectionName = "AppSettings";
});

// 方式二：简化版本
services.AddSyncConfiguration<AppSettings>("appsettings.json", "development");

// 方式三：传入已有的 IConfiguration
services.AddSyncConfiguration<AppSettings>(configuration, sectionName: "AppSettings");
```

### 在服务中使用

```csharp
public class MyService
{
    private readonly ISyncConfiguration<AppSettings> _config;

    public MyService(ISyncConfiguration<AppSettings> config)
    {
        _config = config;

        // 读取配置
        var appName = _config.Value.AppName;

        // 修改并保存
        _config.Update(c => c.AppName = "NewName");

        // 监听变更
        _config.ConfigurationChanged += OnConfigChanged;
    }

    private void OnConfigChanged(object? sender, ConfigurationChangedEventArgs<AppSettings> e)
    {
        Console.WriteLine($"配置变更: {e.Source}");
    }
}
```

## 简单值同步（无需定义配置类）

对于简单的配置项，不需要定义配置类，可以直接使用 `SyncValue<T>`：

```csharp
// 同步单个配置项
using var port = SyncConfig.CreateValue<int>("appsettings.json", "Server:Port", defaultValue: 8080);
using var apiKey = SyncConfig.CreateValue<string>("appsettings.json", "ApiKey", defaultValue: "");

// 读取
Console.WriteLine(port.Value); // 8080

// 修改（自动保存到文件）
port.Update(9000);

// 支持嵌套路径
using var timeout = SyncConfig.CreateValue<int>("appsettings.json", "Database:Connection:Timeout", defaultValue: 30);

// 监听变更
port.ValueChanged += (s, e) => Console.WriteLine($"端口变更: {e.OldValue} -> {e.NewValue}");
```

## 性能优化

### 无锁读取

`Value` 属性使用 `volatile` 关键字，高并发读取无锁竞争。

### 写入防抖

默认 100ms 防抖，多次连续修改只会写入一次文件：

```csharp
// 这三次修改只会触发一次文件写入
config.Update(c => c.Port = 8080);
config.Update(c => c.Port = 8081);
config.Update(c => c.Port = 8082);

// 禁用防抖（每次立即保存）
var config = SyncConfig.Create<AppSettings>(opts => {
    opts.FilePath = "appsettings.json";
    opts.SaveDebounceMs = 0;
});
```

### 批量更新

```csharp
// 多次调用自动合并保存
config.BatchUpdate(c => c.Port = 9000);
config.BatchUpdate(c => c.AppName = "NewName");
config.BatchUpdate(c => c.Timeout = 30);
// 只会触发一次文件写入
```

### 使用 MemoryPack 加速克隆

默认使用 JSON 进行对象克隆（用于变更事件的新旧值比较）。如果追求极致性能，可以切换到 MemoryPack（快 10-50 倍）：

```csharp
// 1. 配置类需要添加 [MemoryPackable] 特性
[MemoryPackable]
public partial class AppSettings
{
    public string AppName { get; set; } = "MyApp";
    public int Port { get; set; } = 8080;
}

// 2. 创建配置时指定使用 MemoryPack
var config = SyncConfig.Create<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
    options.CloneSerializer = CloneSerializerType.MemoryPack;
});
```

**注意**：
- 文件存储始终使用 JSON（保持可读性）
- MemoryPack 仅用于内存中的对象克隆
- 使用 MemoryPack 时，配置类必须添加 `[MemoryPackable]` 特性并声明为 `partial`

#### 自定义克隆序列化器

也可以实现 `ICloneSerializer` 接口来自定义克隆逻辑：

```csharp
public class MyCloneSerializer : ICloneSerializer
{
    public T? Clone<T>(T value)
    {
        // 自定义克隆逻辑
    }
}

var config = SyncConfig.Create<AppSettings>(options =>
{
    options.FilePath = "appsettings.json";
    options.CustomCloneSerializer = new MyCloneSerializer();
});
```

## 许可证

MIT License
