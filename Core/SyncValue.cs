using System.Text.Json;
using System.Text.Json.Nodes;
using LyuSyncConfiguration.Abstractions;
using LyuSyncConfiguration.Options;

namespace LyuSyncConfiguration.Core;

/// <summary>
/// 简单值同步实现，支持单个配置项的双向同步
/// </summary>
/// <typeparam name="T">值类型</typeparam>
public class SyncValue<T> : ISyncValue<T>
{
    private readonly string _filePath;
    private readonly string _key;
    private readonly T _defaultValue;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly object _writeLock = new();
    private readonly int _saveDebounceMs;

    private T _value;
    private readonly object _readLock = new();
    private bool _disposed;
    private volatile bool _isSaving;
    private Timer? _saveDebounceTimer;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private const int IgnoreFileChangeAfterSaveMs = 500;

    /// <inheritdoc/>
    public T Value
    {
        get
        {
            lock (_readLock)
            {
                return _value;
            }
        }
    }

    /// <inheritdoc/>
    public string Key => _key;

    /// <inheritdoc/>
    public string FilePath => _filePath;

    /// <inheritdoc/>
    public event EventHandler<ValueChangedEventArgs<T>>? ValueChanged;

    /// <summary>
    /// 创建简单值同步实例
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="key">配置键路径（支持冒号分隔的嵌套路径，如 "Server:Port"）</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="options">可选配置选项</param>
    public SyncValue(string filePath, string key, T defaultValue, SyncConfigurationOptions? options = null)
    {
        options ??= new SyncConfigurationOptions();

        _filePath = Path.GetFullPath(filePath);
        _key = key;
        _defaultValue = defaultValue;
        _value = defaultValue;
        _jsonOptions = options.JsonOptions;
        _saveDebounceMs = options.SaveDebounceMs;

        EnsureFileExists();
        LoadFromFile();

        if (options.EnableFileWatcher)
        {
            _fileWatcher = CreateFileWatcher();
        }
    }

    /// <inheritdoc/>
    public void Update(T newValue)
    {
        lock (_writeLock)
        {
            var oldValue = _value;
            _value = newValue;
            ScheduleDebouncedSave(oldValue);
        }
    }

    /// <inheritdoc/>
    public void Reload()
    {
        lock (_writeLock)
        {
            var oldValue = _value;
            LoadFromFile();
            OnValueChanged(oldValue, _value, ConfigurationChangeSource.FileLoad);
        }
    }

    private void EnsureFileExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_filePath))
        {
            // 创建包含默认值的文件
            SaveToFileInternal();
        }
    }

    private void LoadFromFile()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            var root = JsonNode.Parse(json);
            if (root == null)
            {
                _value = _defaultValue;
                return;
            }

            var node = GetJsonNode(root, _key);
            if (node == null)
            {
                _value = _defaultValue;
                return;
            }

            _value = node.Deserialize<T>(_jsonOptions) ?? _defaultValue;
        }
        catch
        {
            _value = _defaultValue;
        }
    }

    private void SaveToFileInternal()
    {
        _isSaving = true;
        try
        {
            JsonNode root;
            if (File.Exists(_filePath))
            {
                var existingJson = File.ReadAllText(_filePath);
                root = JsonNode.Parse(existingJson) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            SetJsonNode(root, _key, _value);

            var options = new JsonSerializerOptions(_jsonOptions) { WriteIndented = true };
            var json = root.ToJsonString(options);
            File.WriteAllText(_filePath, json);
            _lastSaveTime = DateTime.UtcNow;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static JsonNode? GetJsonNode(JsonNode root, string key)
    {
        var parts = key.Split(':');
        JsonNode? current = root;

        foreach (var part in parts)
        {
            if (current is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(part, out current))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private void SetJsonNode(JsonNode root, string key, T value)
    {
        var parts = key.Split(':');
        var current = root as JsonObject;
        if (current == null) return;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (!current.TryGetPropertyValue(part, out var next) || next is not JsonObject)
            {
                next = new JsonObject();
                current[part] = next;
            }
            current = next as JsonObject;
            if (current == null) return;
        }

        var lastPart = parts[^1];
        var valueNode = JsonSerializer.SerializeToNode(value, _jsonOptions);
        current[lastPart] = valueNode;
    }

    private FileSystemWatcher CreateFileWatcher()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        var fileName = Path.GetFileName(_filePath);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnFileChanged;

        return watcher;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 如果正在保存，忽略
        if (_isSaving) return;
        
        // 如果在保存后的时间窗口内，忽略（防止自己保存触发的事件）
        if ((DateTime.UtcNow - _lastSaveTime).TotalMilliseconds < IgnoreFileChangeAfterSaveMs) return;

        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_writeLock)
            {
                // 再次检查，防止延迟期间状态变化
                if (_disposed || _isSaving) return;
                if ((DateTime.UtcNow - _lastSaveTime).TotalMilliseconds < IgnoreFileChangeAfterSaveMs) return;

                try
                {
                    var oldValue = _value;
                    LoadFromFile();
                    OnValueChanged(oldValue, _value, ConfigurationChangeSource.FileWatch);
                }
                catch
                {
                    // 忽略加载错误，保持当前值
                }
            }
        });
    }

    private void ScheduleDebouncedSave(T oldValue)
    {
        _saveDebounceTimer?.Dispose();

        if (_saveDebounceMs <= 0)
        {
            ExecuteSave(oldValue);
        }
        else
        {
            var capturedOldValue = oldValue;
            _saveDebounceTimer = new Timer(_ =>
            {
                lock (_writeLock)
                {
                    if (!_disposed)
                    {
                        ExecuteSave(capturedOldValue);
                    }
                }
            }, null, _saveDebounceMs, Timeout.Infinite);
        }
    }

    private void ExecuteSave(T oldValue)
    {
        SaveToFileInternal();
        OnValueChanged(oldValue, _value, ConfigurationChangeSource.CodeUpdate);
    }

    private void OnValueChanged(T? oldValue, T newValue, ConfigurationChangeSource source)
    {
        ValueChanged?.Invoke(this, new ValueChangedEventArgs<T>(oldValue, newValue, source));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_writeLock)
        {
            _disposed = true;

            if (_saveDebounceTimer != null)
            {
                _saveDebounceTimer.Dispose();
                _saveDebounceTimer = null;
                SaveToFileInternal();
            }

            _fileWatcher?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
