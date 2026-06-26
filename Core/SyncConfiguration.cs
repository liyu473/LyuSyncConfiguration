using System.Text.Json;
using System.Text.Json.Nodes;
using LyuSyncConfiguration.Abstractions;
using LyuSyncConfiguration.Options;
using LyuSyncConfiguration.Serializers;
using Microsoft.Extensions.Configuration;

namespace LyuSyncConfiguration.Core;

/// <summary>
/// 同步配置实现类，支持配置文件与内存的双向同步。
/// </summary>
/// <typeparam name="T">配置类型</typeparam>
public class SyncConfiguration<T> : ISyncConfiguration<T>
    where T : class, new()
{
    private readonly string _filePath;
    private readonly string? _environmentFilePath;
    private readonly string? _sectionName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly FileSystemWatcher? _envFileWatcher;
#if NET9_0_OR_GREATER
    private readonly Lock _writeLock = new();
#else
    private readonly object _writeLock = new();
#endif

    private readonly int _saveDebounceMs;
    private readonly ICloneSerializer _cloneSerializer;

    private volatile T _value;
    private IConfigurationRoot? _configuration;
    private bool _disposed;
    private volatile bool _isSaving;
    private Timer? _saveDebounceTimer;
    private T? _pendingOldValue;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private const int IgnoreFileChangeAfterSaveMs = 500; // 保存后忽略文件变化的时间窗口

    /// <inheritdoc/>
    public T Value => _value;

    /// <inheritdoc/>
    public string FilePath => _filePath;

    /// <inheritdoc/>
    public event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;

    /// <summary>
    /// 创建同步配置实例。
    /// </summary>
    /// <param name="options">配置选项</param>
    public SyncConfiguration(SyncConfigurationOptions options)
    {
        _filePath = Path.GetFullPath(options.FilePath);
        _sectionName = options.SectionName;
        _jsonOptions = options.JsonOptions;
        _saveDebounceMs = options.SaveDebounceMs;

        // 初始化克隆序列化器
        _cloneSerializer =
            options.CustomCloneSerializer
            ?? options.CloneSerializer switch
            {
                CloneSerializerType.MemoryPack => new MemoryPackCloneSerializer(),
                _ => new JsonCloneSerializer(options.JsonOptions),
            };

        // 构建环境配置文件路径
        if (!string.IsNullOrEmpty(options.Environment))
        {
            var directory = Path.GetDirectoryName(_filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_filePath);
            var extension = Path.GetExtension(_filePath);
            _environmentFilePath = Path.Combine(
                directory ?? string.Empty,
                $"{fileNameWithoutExt}.{options.Environment}{extension}"
            );
        }

        _value = new T();

        EnsureFileExists();
        LoadFromFile();

        if (options.EnableFileWatcher)
        {
            _fileWatcher = CreateFileWatcher(_filePath);
            if (_environmentFilePath != null && File.Exists(_environmentFilePath))
            {
                _envFileWatcher = CreateFileWatcher(_environmentFilePath);
            }
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        lock (_writeLock)
        {
            SaveToFileInternal();
        }
    }

    /// <inheritdoc/>
    public void Reload()
    {
        lock (_writeLock)
        {
            var oldValue = CloneValue(_value);
            LoadFromFile();
            OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.FileLoad);
        }
    }

    /// <inheritdoc/>
    public void Update(Action<T> updateAction)
    {
        lock (_writeLock)
        {
            // 记录旧值用于事件通知
            _pendingOldValue ??= CloneValue(_value);

            updateAction(_value);

            // 使用防抖延迟保存
            ScheduleDebouncedSave();
        }
    }

    /// <inheritdoc/>
    public void BatchUpdate(Action<T> updateAction)
    {
        Update(updateAction); // 利用防抖机制，批量更新自动合并保存
    }

    /// <inheritdoc/>
    public void UpdateFragment<TValue>(string keyPath, TValue value)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new ArgumentException("片段路径不能为空。", nameof(keyPath));
        }

        lock (_writeLock)
        {
            var oldValue = CloneValue(_value);
            SaveFragmentToFileInternal(keyPath, value);
            LoadFromFile();
            OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.CodeUpdate);
        }
    }

    /// <summary>
    /// 立即保存，绕过防抖。
    /// </summary>
    public void SaveImmediate()
    {
        lock (_writeLock)
        {
            _saveDebounceTimer?.Dispose();
            _saveDebounceTimer = null;
            ExecuteSave();
        }
    }

    private void ScheduleDebouncedSave()
    {
        _saveDebounceTimer?.Dispose();

        if (_saveDebounceMs <= 0)
        {
            // 无防抖，立即保存
            ExecuteSave();
        }
        else
        {
            _saveDebounceTimer = new Timer(
                _ =>
                {
                    lock (_writeLock)
                    {
                        if (!_disposed)
                        {
                            ExecuteSave();
                        }
                    }
                },
                null,
                _saveDebounceMs,
                Timeout.Infinite
            );
        }
    }

    private void ExecuteSave()
    {
        var oldValue = _pendingOldValue;
        _pendingOldValue = null;

        SaveToFileInternal();
        OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.CodeUpdate);
    }

    private void EnsureFileExists()
    {
        EnsureSingleFileExists(_filePath, createWithDefaults: true);

        if (_environmentFilePath != null)
        {
            EnsureSingleFileExists(_environmentFilePath, createWithDefaults: false);
        }
    }

    private void EnsureSingleFileExists(string path, bool createWithDefaults)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path))
        {
            if (createWithDefaults)
            {
                var defaultValue = new T();
                var json = JsonSerializer.Serialize(defaultValue, _jsonOptions);
                File.WriteAllText(path, json);
            }
            else
            {
                File.WriteAllText(path, "{}");
            }
        }
    }

    private void LoadFromFile()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(_filePath)!)
            .AddJsonFile(Path.GetFileName(_filePath), optional: false, reloadOnChange: false);

        if (_environmentFilePath != null)
        {
            builder.AddJsonFile(
                Path.GetFileName(_environmentFilePath),
                optional: true,
                reloadOnChange: false
            );
        }

        _configuration = builder.Build();

        if (string.IsNullOrEmpty(_sectionName))
        {
            _value = _configuration.Get<T>() ?? new T();
        }
        else
        {
            _value = _configuration.GetSection(_sectionName).Get<T>() ?? new T();
        }
    }

    private void SaveToFileInternal()
    {
        _isSaving = true;
        try
        {
            // 优先保存到环境配置文件（如果存在），否则保存到主配置文件
            var targetFilePath = ResolveTargetFilePath();

            string json;
            if (string.IsNullOrEmpty(_sectionName))
            {
                json = JsonSerializer.Serialize(_value, _jsonOptions);
            }
            else
            {
                var existingJson = File.Exists(targetFilePath)
                    ? File.ReadAllText(targetFilePath)
                    : "{}";
                var document = JsonDocument.Parse(existingJson);
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(
                    stream,
                    new JsonWriterOptions
                    {
                        Indented = true,
                        Encoder = _jsonOptions.Encoder
                    }
                );

                writer.WriteStartObject();

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name != _sectionName)
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WritePropertyName(_sectionName);
                var sectionJson = JsonSerializer.Serialize(_value, _jsonOptions);
                using var sectionDoc = JsonDocument.Parse(sectionJson);
                sectionDoc.RootElement.WriteTo(writer);

                writer.WriteEndObject();
                writer.Flush();

                json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            File.WriteAllText(targetFilePath, json);
            _lastSaveTime = DateTime.UtcNow; // 记录保存时间
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void SaveFragmentToFileInternal<TValue>(string keyPath, TValue value)
    {
        _isSaving = true;
        try
        {
            var targetFilePath = ResolveTargetFilePath();
            var existingJson = File.Exists(targetFilePath)
                ? File.ReadAllText(targetFilePath)
                : "{}";

            JsonNode root = JsonNode.Parse(existingJson) ?? new JsonObject();
            JsonNode currentRoot = GetOrCreateSectionRoot(root);
            SetJsonNode(currentRoot, keyPath, value);

            var options = new JsonSerializerOptions(_jsonOptions)
            {
                WriteIndented = true
            };

            var json = root.ToJsonString(options);
            File.WriteAllText(targetFilePath, json);
            _lastSaveTime = DateTime.UtcNow;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private string ResolveTargetFilePath()
    {
        return (_environmentFilePath != null && File.Exists(_environmentFilePath))
            ? _environmentFilePath
            : _filePath;
    }

    private JsonNode GetOrCreateSectionRoot(JsonNode root)
    {
        if (string.IsNullOrEmpty(_sectionName))
        {
            return root;
        }

        if (root is not JsonObject rootObject)
        {
            throw new InvalidOperationException("根节点必须是 JSON 对象。");
        }

        if (rootObject[_sectionName] is null)
        {
            rootObject[_sectionName] = new JsonObject();
        }

        return rootObject[_sectionName]!;
    }

    private void SetJsonNode<TValue>(JsonNode root, string keyPath, TValue value)
    {
        var parts = keyPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("片段路径不能为空。", nameof(keyPath));
        }

        JsonNode current = root;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var nextPart = parts[i + 1];
            var nextShouldBeArray = int.TryParse(nextPart, out _);

            if (int.TryParse(part, out var arrayIndex))
            {
                if (current is not JsonArray currentArray)
                {
                    throw new InvalidOperationException($"路径片段 {part} 期望数组节点。");
                }

                EnsureArraySize(currentArray, arrayIndex);
                currentArray[arrayIndex] ??= nextShouldBeArray ? new JsonArray() : new JsonObject();
                current = currentArray[arrayIndex]!;
            }
            else
            {
                if (current is not JsonObject currentObject)
                {
                    throw new InvalidOperationException($"路径片段 {part} 期望对象节点。");
                }

                currentObject[part] ??= nextShouldBeArray ? new JsonArray() : new JsonObject();
                current = currentObject[part]!;
            }
        }

        var lastPart = parts[^1];
        var valueNode = JsonSerializer.SerializeToNode(value, _jsonOptions);

        if (int.TryParse(lastPart, out var lastIndex))
        {
            if (current is not JsonArray currentArray)
            {
                throw new InvalidOperationException($"路径片段 {lastPart} 期望数组节点。");
            }

            EnsureArraySize(currentArray, lastIndex);
            currentArray[lastIndex] = valueNode;
        }
        else
        {
            if (current is not JsonObject currentObject)
            {
                throw new InvalidOperationException($"路径片段 {lastPart} 期望对象节点。");
            }

            currentObject[lastPart] = valueNode;
        }
    }

    private static void EnsureArraySize(JsonArray array, int index)
    {
        while (array.Count <= index)
        {
            array.Add(null);
        }
    }

    private T? CloneValue(T value)
    {
        return _cloneSerializer.Clone(value);
    }

    private FileSystemWatcher CreateFileWatcher(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileName(filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;

        return watcher;
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, e);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 如果正在保存，忽略本次变化
        if (_isSaving)
            return;

        // 如果处于保存后的时间窗口内，忽略本次变化，避免自己保存触发事件
        if ((DateTime.UtcNow - _lastSaveTime).TotalMilliseconds < IgnoreFileChangeAfterSaveMs)
            return;

        Task.Delay(300)
            .ContinueWith(_ =>
            {
                lock (_writeLock)
                {
                    // 再次检查，防止延迟期间状态变化
                    if (_disposed || _isSaving)
                        return;
                    if (
                        (DateTime.UtcNow - _lastSaveTime).TotalMilliseconds
                        < IgnoreFileChangeAfterSaveMs
                    )
                        return;

                    try
                    {
                        var oldValue = CloneValue(_value);
                        LoadFromFile();

                        if (AreValuesEqual(oldValue, _value))
                            return;

                        OnConfigurationChanged(
                            oldValue,
                            _value,
                            ConfigurationChangeSource.FileWatch
                        );
                    }
                    catch
                    {
                        // 忽略加载错误，保持当前配置
                    }
                }
            });
    }

    private bool AreValuesEqual(T? oldValue, T newValue)
    {
        if (oldValue is null)
            return false;

        var oldJson = JsonSerializer.Serialize(oldValue, _jsonOptions);
        var newJson = JsonSerializer.Serialize(newValue, _jsonOptions);
        return string.Equals(oldJson, newJson, StringComparison.Ordinal);
    }

    private void OnConfigurationChanged(T? oldValue, T newValue, ConfigurationChangeSource source)
    {
        ConfigurationChanged?.Invoke(
            this,
            new ConfigurationChangedEventArgs<T>(oldValue, newValue, source)
        );
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_writeLock)
        {
            _disposed = true;

            // 如果有待保存的内容，立即保存
            if (_saveDebounceTimer != null)
            {
                _saveDebounceTimer.Dispose();
                _saveDebounceTimer = null;
                ExecuteSave();
            }

            _fileWatcher?.Dispose();
            _envFileWatcher?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
