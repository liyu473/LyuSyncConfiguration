# LyuSyncConfiguration

配置文件与内存双向同步库。修改内存自动保存到文件，修改文件自动更新内存。

## 示例配置文件

```json
// appsettings.json
{
  "AppName": "MyApp",
  "Version": "1.0.0",
  "IsDebug": true,
  "MaxRetryCount": 3,
  
  "Server": {
    "Host": "localhost",
    "Port": 8080,
    "UseHttps": false
  },
  
  "Database": {
    "ConnectionString": "Server=localhost;Database=TestApp;",
    "MaxPoolSize": 100
  },
  
  "AllowedExtensions": [".jpg", ".png", ".pdf"],
  "AvailablePorts": [8080, 8081, 8082]
}
```

## 对应的配置类

```csharp
public class AppConfig
{
    // 基本类型
    public string AppName { get; set; } = "MyApp";
    public string Version { get; set; } = "1.0.0";
    public bool IsDebug { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    
    // 嵌套类
    public ServerConfig Server { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    
    // 列表
    public List<string> AllowedExtensions { get; set; } = [".jpg", ".png"];
    public List<int> AvailablePorts { get; set; } = [8080, 8081];
}

public class ServerConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool UseHttps { get; set; } = false;
}

public class DatabaseConfig
{
    public string ConnectionString { get; set; } = "";
    public int MaxPoolSize { get; set; } = 100;
}
```

## 使用方式

### 1. 同步整个配置文件

#### 直接使用

```csharp
using var config = SyncConfig.Create<AppConfig>("appsettings.json");

// 读取
Console.WriteLine(config.Value.AppName);
Console.WriteLine(config.Value.Server.Port);

// 修改（自动保存到文件）
config.Update(c => {
    c.AppName = "NewApp";
    c.Server.Port = 9000;
});

// 监听变更
config.ConfigurationChanged += (s, e) => Console.WriteLine($"配置变更: {e.Source}");
```

#### 依赖注入

```csharp
// 注册
services.AddSyncConfiguration<AppConfig>(options =>
{
    options.FilePath = "appsettings.json";
});

// 使用
public class MyService
{
    public MyService(ISyncConfiguration<AppConfig> config)
    {
        var port = config.Value.Server.Port;
        config.Update(c => c.Server.Port = 9000);
    }
}
```

### 2. 只同步某个节点（使用 SectionName）

只同步 JSON 中的 `Server` 节点，无需定义整个配置类：

#### 直接使用

```csharp
public class ServerConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
}

using var config = SyncConfig.Create<ServerConfig>(options =>
{
    options.FilePath = "appsettings.json";
    options.SectionName = "Server";
});

config.Update(c => c.Port = 9000);  // 只修改 Server 节点
```

#### 依赖注入

```csharp
services.AddSyncConfiguration<ServerConfig>(options =>
{
    options.FilePath = "appsettings.json";
    options.SectionName = "Server";
});
```

### 3. 只同步单个值（无需定义任何类）

使用冒号分隔的路径访问嵌套属性：
- `"AppName"` → 根级别
- `"Server:Port"` → Server 节点下的 Port
- `"Database:ConnectionString"` → Database 节点下的 ConnectionString

#### 直接使用

```csharp
using var port = SyncConfig.CreateValue<int>("appsettings.json", "Server:Port", defaultValue: 8080);

Console.WriteLine(port.Value);  // 读取
port.Update(9000);              // 修改

port.ValueChanged += (s, e) => Console.WriteLine($"端口变更: {e.OldValue} -> {e.NewValue}");
```

#### 依赖注入

```csharp
// 注册
services.AddSyncValue<int>("appsettings.json", "Server:Port", defaultValue: 8080);

// 同类型多个实例使用 Keyed Services
services.AddKeyedSyncValue<int>("port", "appsettings.json", "Server:Port", 8080);
services.AddKeyedSyncValue<int>("poolSize", "appsettings.json", "Database:MaxPoolSize", 100);

// 使用
public class MyService
{
    public MyService(ISyncValue<int> port) { }
    
    // Keyed Services
    public MyService(
        [FromKeyedServices("port")] ISyncValue<int> port,
        [FromKeyedServices("poolSize")] ISyncValue<int> poolSize) { }
}
```

## 配置选项

| 选项 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `FilePath` | string | `appsettings.json` | 配置文件路径 |
| `Environment` | string? | 自动检测 | 环境名称，会额外加载并优先使用 `appsettings.{Environment}.json` |
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

### Environment 多环境配置

支持按环境加载不同配置文件，环境配置会覆盖主配置中的同名属性。

```json
// appsettings.json（基础配置）
{ "Port": 8080, "IsDebug": false, "AppName": "MyApp" }

// appsettings.development.json（开发环境配置）
{ "Port": 9000, "IsDebug": true }
```

#### 直接使用

```csharp
// 配置类方式
using var config = SyncConfig.Create<AppConfig>(options =>
{
    options.FilePath = "appsettings.json";
    options.Environment = "development";  // 加载 appsettings.development.json
});

// 简单值方式
using var port = SyncConfig.CreateValue<int>("appsettings.json", "Server:Port", 8080, options =>
{
    options.Environment = "development";
});
```

#### 依赖注入

```csharp
services.AddSyncConfiguration<AppConfig>(options =>
{
    options.FilePath = "appsettings.json";
    options.Environment = "development";
});

services.AddSyncValue<int>("appsettings.json", "Server:Port", 8080, options =>
{
    options.Environment = "development";
});
```

#### 自动检测环境

默认开启 `AutoDetectEnvironment = true`，会自动从环境变量读取：
- `DOTNET_ENVIRONMENT`
- `ASPNETCORE_ENVIRONMENT`

```csharp
// 自动检测，无需手动指定 Environment
using var config = SyncConfig.Create<AppConfig>("appsettings.json");
```

设置环境变量：
- Windows CMD: `set DOTNET_ENVIRONMENT=development`
- PowerShell: `$env:DOTNET_ENVIRONMENT="development"`
- launchSettings.json: `"environmentVariables": { "DOTNET_ENVIRONMENT": "development" }`

#### 加载和保存规则

加载顺序：先加载主配置，再加载环境配置（覆盖同名属性）

保存位置：**优先保存到环境配置文件**（如果存在），否则保存到主配置文件

| 场景 | 保存到 |
|------|--------|
| 没有设置 Environment | `appsettings.json` |
| 设置了 Environment，环境文件不存在 | `appsettings.json` |
| 设置了 Environment，环境文件存在 | `appsettings.{Environment}.json` |

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
