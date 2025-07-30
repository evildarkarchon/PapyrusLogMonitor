using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PapyrusMonitor.Core.Configuration;

/// <summary>
/// JSON-based implementation of settings service
/// </summary>
public class JsonSettingsService : ISettingsService, IDisposable
{
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly Subject<AppSettings> _settingsChangedSubject;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _settingsLock = new();
    private AppSettings _currentSettings;
    private bool _isDisposed;

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        _settingsChangedSubject = new Subject<AppSettings>();
        _currentSettings = new AppSettings();
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Setup file watcher for hot-reload
        var directory = Path.GetDirectoryName(SettingsFilePath);
        var fileName = Path.GetFileName(SettingsFilePath);
        
        if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                
                _fileWatcher.Changed += OnSettingsFileChanged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to setup file watcher for settings hot-reload");
            }
        }
    }

    public AppSettings Settings
    {
        get
        {
            lock (_settingsLock)
            {
                return _currentSettings;
            }
        }
    }

    public IObservable<AppSettings> SettingsChanged => _settingsChangedSubject.AsObservable();

    public string SettingsFilePath
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDirectory = Path.Combine(appDataPath, "PapyrusMonitor");
            return Path.Combine(appDirectory, "settings.json");
        }
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                _logger.LogInformation("Settings file not found, using defaults");
                var defaultSettings = new AppSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            
            if (settings == null)
            {
                _logger.LogWarning("Failed to deserialize settings, using defaults");
                return new AppSettings();
            }

            lock (_settingsLock)
            {
                _currentSettings = settings;
            }

            _settingsChangedSubject.OnNext(settings);
            _logger.LogInformation("Settings loaded successfully from {Path}", SettingsFilePath);
            
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", SettingsFilePath);
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            
            // Disable file watcher temporarily to avoid triggering reload
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
            }

            try
            {
                await File.WriteAllTextAsync(SettingsFilePath, json);
            }
            finally
            {
                if (_fileWatcher != null)
                {
                    // Re-enable after a short delay to avoid catching our own write
                    await Task.Delay(100);
                    _fileWatcher.EnableRaisingEvents = true;
                }
            }

            lock (_settingsLock)
            {
                _currentSettings = settings;
            }

            _settingsChangedSubject.OnNext(settings);
            _logger.LogInformation("Settings saved successfully to {Path}", SettingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", SettingsFilePath);
            throw;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        var defaultSettings = new AppSettings();
        await SaveSettingsAsync(defaultSettings);
    }

    private async void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce file changes
        await Task.Delay(100);
        
        try
        {
            _logger.LogDebug("Settings file changed, reloading...");
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload settings after file change");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _fileWatcher?.Dispose();
        _settingsChangedSubject.OnCompleted();
        _settingsChangedSubject.Dispose();
        
        _isDisposed = true;
    }
}