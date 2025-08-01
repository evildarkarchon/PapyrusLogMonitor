using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly ISchedulerProvider _schedulerProvider;
    private readonly ISettingsService _settingsService;
    private readonly IStorageProvider? _storageProvider;

    public SettingsViewModel(ISettingsService settingsService, ISchedulerProvider schedulerProvider, ILogger logger,
        IStorageProvider? storageProvider = null)
    {
        _settingsService = settingsService;
        _storageProvider = storageProvider;
        _schedulerProvider = schedulerProvider;
        _logger = logger;

        // Load current settings
        LoadCurrentSettings(_settingsService.Settings);

        // Subscribe to settings changes (hot-reload)
        _settingsService.SettingsChanged
            .ObserveOn(_schedulerProvider.MainThread)
            .Subscribe(LoadCurrentSettings);

        // Track changes
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
            .Subscribe(_ => HasChanges = true);

        // Create commands
        var canSave = this.WhenAnyValue(x => x.HasChanges, x => x.IsSaving,
            (hasChanges, isSaving) => hasChanges && !isSaving);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync, canSave);

        var canCancel = this.WhenAnyValue(x => x.HasChanges, x => x.IsSaving,
            (hasChanges, isSaving) => hasChanges && !isSaving);
        CancelCommand = ReactiveCommand.Create(Cancel, canCancel);

        var canReset = this.WhenAnyValue(x => x.IsSaving, isSaving => !isSaving);
        ResetToDefaultsCommand = ReactiveCommand.CreateFromTask(ResetToDefaultsAsync, canReset);

        BrowseLogFileCommand = ReactiveCommand.CreateFromTask(BrowseLogFileAsync);
        BrowseExportPathCommand = ReactiveCommand.CreateFromTask(BrowseExportPathAsync);

        // Handle command errors
        SaveCommand.ThrownExceptions.Subscribe(ex =>
            _logger.LogError($"Error saving settings: {ex.Message}", ex));
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
    }

    private async Task SaveSettingsAsync()
    {
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
}
