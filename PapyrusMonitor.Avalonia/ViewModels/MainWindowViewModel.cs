using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IExportService _exportService;
    private readonly ILogger _logger;
    private readonly ISchedulerProvider _schedulerProvider;
    private readonly ISessionHistoryService _sessionHistoryService;
    private readonly ISettingsService _settingsService;
    private readonly IStorageProvider? _storageProvider;
    private readonly ITrendAnalysisService _trendAnalysisService;

    private string _title = "Papyrus Log Monitor";

    public MainWindowViewModel(
        PapyrusMonitorViewModel papyrusMonitorViewModel,
        ISettingsService settingsService,
        IExportService exportService,
        ISessionHistoryService sessionHistoryService,
        ITrendAnalysisService trendAnalysisService,
        ISchedulerProvider schedulerProvider,
        ILogger logger,
        IStorageProvider? storageProvider = null)
    {
        PapyrusMonitorViewModel =
            papyrusMonitorViewModel ?? throw new ArgumentNullException(nameof(papyrusMonitorViewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _sessionHistoryService =
            sessionHistoryService ?? throw new ArgumentNullException(nameof(sessionHistoryService));
        _trendAnalysisService = trendAnalysisService ?? throw new ArgumentNullException(nameof(trendAnalysisService));
        _schedulerProvider = schedulerProvider ?? throw new ArgumentNullException(nameof(schedulerProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageProvider = storageProvider;

        ExitCommand = ReactiveCommand.Create(() =>
        {
            // Exit logic will be handled by the view
        });

        ShowSettingsCommand = ReactiveCommand.Create(() =>
        {
            SettingsViewModel = new SettingsViewModel(_settingsService, _schedulerProvider, _logger, _storageProvider);
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

        // Trend analysis commands
        var canShowTrends = this.WhenAnyValue(x => x._sessionHistoryService.IsSessionActive);
        ShowTrendAnalysisCommand = ReactiveCommand.CreateFromTask(ShowTrendAnalysisAsync, canShowTrends);

        CloseTrendAnalysisCommand = ReactiveCommand.Create(() =>
        {
            ShowTrendAnalysis = false;
            TrendAnalysisViewModel = null;
        });

        // Handle errors
        ExportCommand.ThrownExceptions.Subscribe(ex =>
            Console.WriteLine($"Export failed: {ex.Message}"));
        ShowTrendAnalysisCommand.ThrownExceptions.Subscribe(ex =>
            Console.WriteLine($"Failed to show trend analysis: {ex.Message}"));
    }

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    [Reactive] public bool IsExporting { get; private set; }
    [Reactive] public bool ShowSettings { get; set; }
    [Reactive] public bool ShowTrendAnalysis { get; set; }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseSettingsCommand { get; }
    public ReactiveCommand<ExportFormat, Unit> ExportCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowTrendAnalysisCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseTrendAnalysisCommand { get; }

    public PapyrusMonitorViewModel PapyrusMonitorViewModel { get; }
    public SettingsViewModel? SettingsViewModel { get; private set; }
    public TrendAnalysisViewModel? TrendAnalysisViewModel { get; private set; }

    private async Task ShowTrendAnalysisAsync()
    {
        TrendAnalysisViewModel = new TrendAnalysisViewModel(_sessionHistoryService, _trendAnalysisService);
        ShowTrendAnalysis = true;

        // Trigger initial analysis
        await TrendAnalysisViewModel.RefreshCommand.Execute();
    }

    private async Task ExportDataAsync(ExportFormat format)
    {
        if (_storageProvider == null)
        {
            return;
        }

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
                FileTypeChoices =
                [
                    new FilePickerFileType(format == ExportFormat.Csv ? "CSV Files" : "JSON Files")
                    {
                        Patterns = [$"*{extension}"]
                    }
                ]
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
        Observable.FromAsync(() => _settingsService.LoadSettingsAsync())
            .Subscribe(_ => { }, ex => Console.WriteLine($"Failed to load settings: {ex.Message}"))
            .DisposeWith(disposables);

        // Ensure cleanup when deactivated
        Disposable.Create(() =>
        {
            PapyrusMonitorViewModel.Dispose();
        }).DisposeWith(disposables);
    }
}
