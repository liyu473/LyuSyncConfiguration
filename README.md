# LyuSyncConfiguration

基于微软配置库的配置文件与内存双向同步库。

## 功能特性

- **双向同步**：配置类修改后自动保存到文件，文件修改后自动更新内存
- **文件监控**：支持自动监听配置文件变更
- **多环境支持**：支持 `appsettings.json` + `appsettings.{environment}.json`
- **类型安全**：基于泛型的强类型配置

## 项目结构

```
LyuSyncConfiguration/
├── Abstractions/           # 接口定义
│   └── ISyncConfiguration.cs
├── Core/                   # 核心实现
│   └── SyncConfiguration.cs
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

## 许可证

MIT License
