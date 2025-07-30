using System;
using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using PapyrusLogMonitor.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Services;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.IO;
using System.Reactive.Linq;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IExportService _exportService;
    private readonly ISessionHistoryService _sessionHistoryService;
    private readonly IStorageProvider? _storageProvider;
    
    private string _title = "Papyrus Log Monitor";

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    [Reactive] public bool IsExporting { get; private set; }
    [Reactive] public bool ShowSettings { get; set; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseSettingsCommand { get; }
    public ReactiveCommand<ExportFormat, Unit> ExportCommand { get; }

    public PapyrusMonitorViewModel PapyrusMonitorViewModel { get; }
    public SettingsViewModel? SettingsViewModel { get; private set; }

    public MainWindowViewModel(
        PapyrusMonitorViewModel papyrusMonitorViewModel,
        ISettingsService settingsService,
        IExportService exportService,
        ISessionHistoryService sessionHistoryService,
        IStorageProvider? storageProvider = null)
    {
        PapyrusMonitorViewModel = papyrusMonitorViewModel ?? throw new ArgumentNullException(nameof(papyrusMonitorViewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _sessionHistoryService = sessionHistoryService ?? throw new ArgumentNullException(nameof(sessionHistoryService));
        _storageProvider = storageProvider;
        
        ExitCommand = ReactiveCommand.Create(() =>
        {
            // Exit logic will be handled by the view
        });

        ShowSettingsCommand = ReactiveCommand.Create(() =>
        {
            SettingsViewModel = new SettingsViewModel(_settingsService, _storageProvider);
            ShowSettings = true;
        });

        CloseSettingsCommand = ReactiveCommand.Create(() =>
        {
            ShowSettings = false;
            SettingsViewModel = null;
        });

        var canExport = this.WhenAnyValue(x => x.IsExporting, x => x._sessionHistoryService.IsSessionActive,
            (exporting, active) => !exporting && active);
        ExportCommand = ReactiveCommand.CreateFromTask<ExportFormat>(ExportDataAsync, canExport);

        // Handle export errors
        ExportCommand.ThrownExceptions.Subscribe(ex => 
            Console.WriteLine($"Export failed: {ex.Message}"));
    }

    private async Task ExportDataAsync(ExportFormat format)
    {
        if (_storageProvider == null) return;

        IsExporting = true;
        try
        {
            var extension = _exportService.GetFileExtension(format);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var defaultFileName = $"papyrus_stats_{timestamp}{extension}";

            var options = new FilePickerSaveOptions
            {
                Title = $"Export Statistics as {format}",
                SuggestedFileName = defaultFileName,
                DefaultExtension = extension,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(format == ExportFormat.Csv ? "CSV Files" : "JSON Files")
                    {
                        Patterns = new[] { $"*{extension}" }
                    }
                }
            };

            // Use default export path from settings if available
            var defaultPath = _settingsService.Settings.ExportSettings.DefaultExportPath;
            if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(defaultPath);
            }

            var file = await _storageProvider.SaveFilePickerAsync(options);
            if (file != null)
            {
                var exportData = new ExportData
                {
                    Metadata = new ExportMetadata
                    {
                        LogFilePath = _settingsService.Settings.LogFilePath,
                        SessionStartTime = _sessionHistoryService.SessionStartTime,
                        SessionEndTime = _sessionHistoryService.SessionEndTime
                    },
                    Statistics = _sessionHistoryService.GetSessionStatistics(),
                    Summary = _sessionHistoryService.GetSessionSummary()
                };

                await _exportService.ExportAsync(exportData, file.Path.LocalPath, format);
            }
        }
        finally
        {
            IsExporting = false;
        }
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Activate the child view model
        PapyrusMonitorViewModel.Activator.Activate().DisposeWith(disposables);
        
        // Load settings on activation
        _settingsService.LoadSettingsAsync()
            .Subscribe(_ => { }, ex => Console.WriteLine($"Failed to load settings: {ex.Message}"))
            .DisposeWith(disposables);
        
        // Ensure cleanup when deactivated
        Disposable.Create(() =>
        {
            PapyrusMonitorViewModel?.Dispose();
        }).DisposeWith(disposables);
    }
}