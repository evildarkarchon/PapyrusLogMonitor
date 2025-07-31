using System.ComponentModel.DataAnnotations;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.ViewModels;

/// <summary>
/// Validated version of SettingsViewModel that demonstrates comprehensive validation scenarios
/// for testing purposes. This includes various validation attributes, cross-property validation,
/// and async validation scenarios.
/// </summary>
public class SettingsViewModelValidated : ValidationViewModelBase
{
    private readonly ILogger _logger;
    private readonly ISchedulerProvider _schedulerProvider;
    private readonly ISettingsService _settingsService;
    private readonly IStorageProvider? _storageProvider;

    public SettingsViewModelValidated(ISettingsService settingsService, ISchedulerProvider schedulerProvider, 
        ILogger logger, IStorageProvider? storageProvider = null)
    {
        _settingsService = settingsService;
        _storageProvider = storageProvider;
        _schedulerProvider = schedulerProvider;
        _logger = logger;

        // Load current settings
        LoadCurrentSettings(_settingsService.Settings);

        // Register validation rules
        RegisterValidationRules();

        // All subscriptions will be properly disposed in HandleActivation

        // Create commands with validation checks
        var canSave = this.WhenAnyValue(x => x.HasChanges, x => x.IsSaving, x => x.HasErrors,
            (hasChanges, isSaving, hasErrors) => hasChanges && !isSaving && !hasErrors);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync, canSave);

        var canCancel = this.WhenAnyValue(x => x.HasChanges, x => x.IsSaving,
            (hasChanges, isSaving) => hasChanges && !isSaving);
        CancelCommand = ReactiveCommand.Create(Cancel, canCancel);

        var canReset = this.WhenAnyValue(x => x.IsSaving, isSaving => !isSaving);
        ResetToDefaultsCommand = ReactiveCommand.CreateFromTask(ResetToDefaultsAsync, canReset);

        BrowseLogFileCommand = ReactiveCommand.CreateFromTask(BrowseLogFileAsync);
        BrowseExportPathCommand = ReactiveCommand.CreateFromTask(BrowseExportPathAsync);
        ValidateAllCommand = ReactiveCommand.CreateFromTask(ValidateAllAsync);

        // Command error handling will be set up in HandleActivation
    }

    [Reactive] public string LogFilePath { get; set; } = string.Empty;
    [Reactive] public int UpdateInterval { get; set; } = 1000;
    [Reactive] public bool AutoStartMonitoring { get; set; }
    [Reactive] public int MaxLogEntries { get; set; } = 10000;
    [Reactive] public bool ShowErrorNotifications { get; set; } = true;
    [Reactive] public bool ShowWarningNotifications { get; set; }
    [Reactive] public string DefaultExportPath { get; set; } = string.Empty;
    [Reactive] public bool IncludeTimestamps { get; set; } = true;
    [Reactive] public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    [Reactive] public bool IsSaving { get; private set; }
    [Reactive] public bool HasChanges { get; private set; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseLogFileCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseExportPathCommand { get; }
    public ReactiveCommand<Unit, bool> ValidateAllCommand { get; }

    private void RegisterValidationRules()
    {
        // Basic validation attributes
        RegisterPropertyValidation(nameof(LogFilePath), 
            new RequiredAttribute { ErrorMessage = "Log file path is required." },
            new CustomValidationAttribute(typeof(SettingsViewModelValidated), nameof(ValidateLogFileExtension)));

        RegisterPropertyValidation(nameof(UpdateInterval),
            new RangeAttribute(100, 10000) { ErrorMessage = "Update interval must be between 100 and 10000 milliseconds." });

        RegisterPropertyValidation(nameof(MaxLogEntries),
            new RangeAttribute(1000, 100000) { ErrorMessage = "Max log entries must be between 1000 and 100000." });

        RegisterPropertyValidation(nameof(DateFormat),
            new RequiredAttribute { ErrorMessage = "Date format is required." },
            new CustomValidationAttribute(typeof(SettingsViewModelValidated), nameof(ValidateDateFormat)));

        // Async validation for file system operations
        RegisterAsyncPropertyValidation(nameof(LogFilePath), ValidateLogFilePathAsync);
        RegisterAsyncPropertyValidation(nameof(DefaultExportPath), ValidateExportPathAsync);
    }

    /// <summary>
    /// Custom validation method for log file extension
    /// </summary>
    public static ValidationResult? ValidateLogFileExtension(string? value, ValidationContext context)
    {
        if (string.IsNullOrEmpty(value))
            return ValidationResult.Success; // Required validation will handle this

        if (!value.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult("Log file must have a .log extension.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Custom validation method for date format
    /// </summary>
    public static ValidationResult? ValidateDateFormat(string? value, ValidationContext context)
    {
        if (string.IsNullOrEmpty(value))
            return ValidationResult.Success; // Required validation will handle this

        try
        {
            // Test the format with a sample date
            // Check for common invalid patterns first
            if (value.Contains("{") || value.Contains("}"))
            {
                return new ValidationResult("Invalid date format string.");
            }
            
            DateTime.Now.ToString(value);
            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult("Invalid date format string.");
        }
    }

    /// <summary>
    /// Async validation for log file path existence and accessibility
    /// </summary>
    private async Task<IEnumerable<string>> ValidateLogFilePathAsync(object? value)
    {
        await Task.Delay(100); // Simulate async operation
        
        var errors = new List<string>();
        var path = value as string;

        if (string.IsNullOrEmpty(path))
            return errors; // Required validation will handle this

        try
        {
            // Check if directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                errors.Add("Log file directory does not exist.");
            }

            // Check if file exists (warn if it doesn't)
            if (!File.Exists(path))
            {
                errors.Add("Warning: Log file does not exist yet.");
            }
            else
            {
                // Check if file is accessible
                try
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add("Access denied to log file.");
                }
                catch (IOException)
                {
                    errors.Add("Log file is in use by another process.");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating log file path: {ex.Message}");
        }

        return errors;
    }

    /// <summary>
    /// Async validation for export path
    /// </summary>
    private async Task<IEnumerable<string>> ValidateExportPathAsync(object? value)
    {
        await Task.Delay(50); // Simulate async operation
        
        var errors = new List<string>();
        var path = value as string;

        if (string.IsNullOrEmpty(path))
            return errors; // Optional field

        try
        {
            if (!Directory.Exists(path))
            {
                errors.Add("Export directory does not exist.");
            }
            else
            {
                // Check write permissions
                var testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.tmp");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add("No write permission to export directory.");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating export path: {ex.Message}");
        }

        return errors;
    }

    private void LoadCurrentSettings(AppSettings settings)
    {
        LogFilePath = settings.LogFilePath;
        UpdateInterval = settings.UpdateInterval;
        AutoStartMonitoring = settings.AutoStartMonitoring;
        MaxLogEntries = settings.MaxLogEntries;
        ShowErrorNotifications = settings.ShowErrorNotifications;
        ShowWarningNotifications = settings.ShowWarningNotifications;
        DefaultExportPath = settings.ExportSettings.DefaultExportPath;
        IncludeTimestamps = settings.ExportSettings.IncludeTimestamps;
        DateFormat = settings.ExportSettings.DateFormat;
        HasChanges = false;
        ClearAllValidationErrors();
    }

    private async Task SaveSettingsAsync()
    {
        // Validate all properties before saving
        var isValid = await ValidateAllPropertiesAsync();
        if (!isValid)
        {
            _logger.LogWarning("Cannot save settings: validation errors exist.");
            return;
        }

        IsSaving = true;
        try
        {
            var settings = new AppSettings
            {
                LogFilePath = LogFilePath,
                UpdateInterval = UpdateInterval,
                AutoStartMonitoring = AutoStartMonitoring,
                MaxLogEntries = MaxLogEntries,
                ShowErrorNotifications = ShowErrorNotifications,
                ShowWarningNotifications = ShowWarningNotifications,
                ExportSettings = new ExportSettings
                {
                    DefaultExportPath = DefaultExportPath,
                    IncludeTimestamps = IncludeTimestamps,
                    DateFormat = DateFormat
                },
                WindowSettings = _settingsService.Settings.WindowSettings // Preserve window settings
            };

            await _settingsService.SaveSettingsAsync(settings);
            HasChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error saving settings", ex);
            throw;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void Cancel()
    {
        LoadCurrentSettings(_settingsService.Settings);
    }

    private async Task ResetToDefaultsAsync()
    {
        IsSaving = true;
        try
        {
            await _settingsService.ResetToDefaultsAsync();
            LoadCurrentSettings(_settingsService.Settings);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task<bool> ValidateAllAsync()
    {
        return await ValidateAllPropertiesAsync();
    }
    
    /// <summary>
    /// Forces immediate validation of a property without waiting for debouncing.
    /// Primarily for testing purposes.
    /// </summary>
    public Task<bool> ForceValidatePropertyAsync(string propertyName)
    {
        var value = GetType().GetProperty(propertyName)?.GetValue(this);
        return ValidatePropertyAsync(propertyName, value);
    }

    private async Task BrowseLogFileAsync()
    {
        if (_storageProvider == null)
        {
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Select Papyrus Log File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Log Files") { Patterns = ["Papyrus.0.log"] },
                FilePickerFileTypes.All
            ]
        };

        var result = await _storageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            LogFilePath = result[0].Path.LocalPath;
        }
    }

    private async Task BrowseExportPathAsync()
    {
        if (_storageProvider == null)
        {
            return;
        }

        var options = new FolderPickerOpenOptions { Title = "Select Export Directory", AllowMultiple = false };

        var result = await _storageProvider.OpenFolderPickerAsync(options);
        if (result.Count > 0)
        {
            DefaultExportPath = result[0].Path.LocalPath;
        }
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        base.HandleActivation(disposables);
        
        // Subscribe to settings changes (hot-reload)
        _settingsService.SettingsChanged
            .ObserveOn(_schedulerProvider.MainThread)
            .Subscribe(LoadCurrentSettings)
            .DisposeWith(disposables);

        // Track changes and trigger validation with debouncing
        this.WhenAnyValue(x => x.LogFilePath)
            .Skip(1) // Skip initial value
            .Throttle(TimeSpan.FromMilliseconds(300), _schedulerProvider.CurrentThread) // Debounce for performance
            .ObserveOn(_schedulerProvider.MainThread)
            .Subscribe(async value => await ValidatePropertyAsync(nameof(LogFilePath), value))
            .DisposeWith(disposables);

        this.WhenAnyValue(x => x.UpdateInterval)
            .Skip(1)
            .Subscribe(async value => await ValidatePropertyAsync(nameof(UpdateInterval), value))
            .DisposeWith(disposables);

        this.WhenAnyValue(x => x.MaxLogEntries)
            .Skip(1)
            .Subscribe(async value => await ValidatePropertyAsync(nameof(MaxLogEntries), value))
            .DisposeWith(disposables);

        this.WhenAnyValue(x => x.DateFormat)
            .Skip(1)
            .Subscribe(async value => await ValidatePropertyAsync(nameof(DateFormat), value))
            .DisposeWith(disposables);

        this.WhenAnyValue(x => x.DefaultExportPath)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(500), _schedulerProvider.CurrentThread) // Debounce file system operations
            .ObserveOn(_schedulerProvider.MainThread)
            .Subscribe(async value => await ValidatePropertyAsync(nameof(DefaultExportPath), value))
            .DisposeWith(disposables);

        // Track changes for HasChanges property
        this.WhenAnyValue(x => x.LogFilePath).CombineLatest(this.WhenAnyValue(x => x.UpdateInterval),
                this.WhenAnyValue(x => x.AutoStartMonitoring),
                this.WhenAnyValue(x => x.MaxLogEntries),
                this.WhenAnyValue(x => x.ShowErrorNotifications),
                this.WhenAnyValue(x => x.ShowWarningNotifications),
                this.WhenAnyValue(x => x.DefaultExportPath),
                this.WhenAnyValue(x => x.IncludeTimestamps),
                this.WhenAnyValue(x => x.DateFormat),
                (_, _, _, _, _, _, _, _, _) => true)
            .Skip(1) // Skip initial values
            .Subscribe(_ => HasChanges = true)
            .DisposeWith(disposables);

        // Handle command errors
        SaveCommand.ThrownExceptions.Subscribe(ex =>
            _logger.LogError($"Error saving settings: {ex.Message}", ex))
            .DisposeWith(disposables);
        
        // Auto-validate on activation
        Task.Run(async () => await ValidateAllPropertiesAsync()).DisposeWith(disposables);
    }
}