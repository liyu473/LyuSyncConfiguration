using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LyuSyncConfiguration.Abstractions;
using LyuSyncConfiguration.Options;

namespace LyuSyncConfiguration.Core;

/// <summary>
/// 同步配置实现类，支持配置文件与内存的双向同步
/// </summary>
/// <typeparam name="T">配置类型</typeparam>
public class SyncConfiguration<T> : ISyncConfiguration<T> where T : class, new()
{
    private readonly string _filePath;
    private readonly string? _environmentFilePath;
    private readonly string? _sectionName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly FileSystemWatcher? _envFileWatcher;
    private readonly object _lock = new();

    private T _value;
    private IConfigurationRoot? _configuration;
    private bool _disposed;
    private bool _isSaving;

    /// <inheritdoc/>
    public T Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
    }

    /// <inheritdoc/>
    public string FilePath => _filePath;

    /// <inheritdoc/>
    public event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;

    /// <summary>
    /// 创建同步配置实例
    /// </summary>
    /// <param name="options">配置选项</param>
    public SyncConfiguration(SyncConfigurationOptions options)
    {
        _filePath = Path.GetFullPath(options.FilePath);
        _sectionName = options.SectionName;
        _jsonOptions = options.JsonOptions;

        // 构建环境配置文件路径
        if (!string.IsNullOrEmpty(options.Environment))
        {
            var directory = Path.GetDirectoryName(_filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_filePath);
            var extension = Path.GetExtension(_filePath);
            _environmentFilePath = Path.Combine(directory ?? "", $"{fileNameWithoutExt}.{options.Environment}{extension}");
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
        lock (_lock)
        {
            SaveToFile();
        }
    }

    /// <inheritdoc/>
    public void Reload()
    {
        lock (_lock)
        {
            var oldValue = CloneValue(_value);
            LoadFromFile();
            OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.FileLoad);
        }
    }

    /// <inheritdoc/>
    public void Update(Action<T> updateAction)
    {
        lock (_lock)
        {
            var oldValue = CloneValue(_value);
            updateAction(_value);
            SaveToFile();
            OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.CodeUpdate);
        }
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
            builder.AddJsonFile(Path.GetFileName(_environmentFilePath), optional: true, reloadOnChange: false);
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

    private void SaveToFile()
    {
        _isSaving = true;
        try
        {
            string json;
            if (string.IsNullOrEmpty(_sectionName))
            {
                json = JsonSerializer.Serialize(_value, _jsonOptions);
            }
            else
            {
                var existingJson = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
                var document = JsonDocument.Parse(existingJson);
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

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

            File.WriteAllText(_filePath, json);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private T? CloneValue(T value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    private FileSystemWatcher CreateFileWatcher(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileName(filePath);

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
        if (_isSaving) return;

        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_lock)
            {
                if (_disposed || _isSaving) return;

                try
                {
                    var oldValue = CloneValue(_value);
                    LoadFromFile();
                    OnConfigurationChanged(oldValue, _value, ConfigurationChangeSource.FileWatch);
                }
                catch
                {
                    // 忽略加载错误，保持当前配置
                }
            }
        });
    }

    private void OnConfigurationChanged(T? oldValue, T newValue, ConfigurationChangeSource source)
    {
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs<T>(oldValue, newValue, source));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            _fileWatcher?.Dispose();
            _envFileWatcher?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
