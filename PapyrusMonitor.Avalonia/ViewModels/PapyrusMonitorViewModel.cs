using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class PapyrusMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly ObservableAsPropertyHelper<string> _errorsStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _errorsStatusColor;
    private readonly ObservableAsPropertyHelper<bool> _hasStatusMessage;
    private readonly ObservableAsPropertyHelper<bool> _isMonitoring;
    private readonly ObservableAsPropertyHelper<DateTime> _lastUpdateTime;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonIcon;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonText;
    private readonly IPapyrusMonitorService _monitorService;
    private readonly ObservableAsPropertyHelper<string> _ratioStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _ratioStatusColor;
    private readonly ISessionHistoryService _sessionHistoryService;
    private readonly ISettingsService _settingsService;
    private readonly ObservableAsPropertyHelper<string> _statusMessage;
    private readonly ObservableAsPropertyHelper<IBrush> _statusMessageBackground;
    private readonly ObservableAsPropertyHelper<FontWeight> _statusMessageFontWeight;
    private readonly ObservableAsPropertyHelper<string> _statusText;
    private readonly ObservableAsPropertyHelper<string> _warningsStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _warningsStatusColor;
    private CancellationTokenSource? _cancellationTokenSource;
    private string _currentButtonIcon = "▶️";
    private string _currentButtonText = "Start Monitoring";
    private bool _enableAnimations = true;
    private bool _isMonitoringInternal;
    private bool _isProcessing;
    private string? _lastError;
    private string? _logFilePath;
    private PapyrusStats _statistics = new(DateTime.Now, 0, 0, 0, 0, 0.0);

    public PapyrusMonitorViewModel(IPapyrusMonitorService monitorService, ISettingsService settingsService,
        ISessionHistoryService sessionHistoryService)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _sessionHistoryService =
            sessionHistoryService ?? throw new ArgumentNullException(nameof(sessionHistoryService));

        // Set initial log path from settings
        LogFilePath = _settingsService.Settings.LogFilePath;

        // Create observable for monitoring state
        var isMonitoringObservable = this.WhenAnyValue(x => x.IsMonitoringInternal)
            .StartWith(IsMonitoringInternal); // Ensure initial value is emitted

        _isMonitoring = isMonitoringObservable
            .ToProperty(this, x => x.IsMonitoring, initialValue: false);

        _statusText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Monitoring..." : "Idle")
            .ToProperty(this, x => x.StatusText, "Idle");

        // Last update time
        _lastUpdateTime = this.WhenAnyValue(x => x.Statistics)
            .Select(stats => stats?.Timestamp ?? DateTime.Now)
            .ToProperty(this, x => x.LastUpdateTime, DateTime.Now);

        // Monitoring button properties
        _monitoringButtonText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Stop Monitoring" : "Start Monitoring")
            .ToProperty(this, x => x.MonitoringButtonText, "Start Monitoring");

        _monitoringButtonIcon = isMonitoringObservable
            .Select(monitoring => monitoring ? "⏹️" : "▶️")
            .ToProperty(this, x => x.MonitoringButtonIcon, "▶️");

        // Status message properties based on statistics
        var statsObservable = this.WhenAnyValue(x => x.Statistics);

        _hasStatusMessage = statsObservable.CombineLatest(this.WhenAnyValue(x => x.LastError),
                (stats, error) => !string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0 ||
                                  stats.Ratio > 0.5)
            .ToProperty(this, x => x.HasStatusMessage, initialValue: false);

        _statusMessage = statsObservable.CombineLatest(this.WhenAnyValue(x => x.LastError),
                (stats, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        return $"Error: {error}";
                    }

                    if (stats.Errors > 0)
                    {
                        return $"{stats.Errors} errors detected in Papyrus log!";
                    }

                    if (stats.Warnings > 0)
                    {
                        return $"{stats.Warnings} warnings detected in Papyrus log.";
                    }

                    if (stats.Ratio > 0.8)
                    {
                        return "Warning: High dumps-to-stacks ratio detected!";
                    }

                    if (stats.Ratio > 0.5)
                    {
                        return "Caution: Elevated dumps-to-stacks ratio.";
                    }

                    return "Papyrus log appears normal.";
                })
            .ToProperty(this, x => x.StatusMessage, "Papyrus log appears normal.");

        _statusMessageBackground = statsObservable.CombineLatest(this.WhenAnyValue(x => x.LastError),
                (stats, error) =>
                {
                    if (!string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Ratio > 0.8)
                    {
                        return new SolidColorBrush(Color.FromRgb(255, 230, 230)); // Light red
                    }

                    if (stats.Warnings > 0 || stats.Ratio > 0.5)
                    {
                        return new SolidColorBrush(Color.FromRgb(255, 244, 229)); // Light orange
                    }

                    return new SolidColorBrush(Color.FromRgb(230, 255, 230)); // Light green
                })
            .ToProperty(this, x => x.StatusMessageBackground, new SolidColorBrush(Color.FromRgb(230, 255, 230)));

        _statusMessageFontWeight = statsObservable.CombineLatest(this.WhenAnyValue(x => x.LastError),
                (stats, error) => !string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0
                    ? FontWeight.Bold
                    : FontWeight.Normal)
            .ToProperty(this, x => x.StatusMessageFontWeight, FontWeight.Normal);

        // Ratio status properties
        _ratioStatus = statsObservable
            .Select(stats => stats.Ratio > 0.8 ? "❌" : stats.Ratio > 0.5 ? "⚠️" : "✓")
            .ToProperty(this, x => x.RatioStatus, "✓");

        _ratioStatusColor = statsObservable
            .Select(stats => stats.Ratio > 0.8
                ? Brushes.Red
                : stats.Ratio > 0.5
                    ? Brushes.Orange
                    : Brushes.Green)
            .ToProperty(this, x => x.RatioStatusColor, Brushes.Green);

        // Warnings status properties
        _warningsStatus = statsObservable
            .Select(stats => stats.Warnings > 0 ? "⚠️" : "✓")
            .ToProperty(this, x => x.WarningsStatus, "✓");

        _warningsStatusColor = statsObservable
            .Select(stats => stats.Warnings > 0 ? Brushes.Orange : Brushes.Green)
            .ToProperty(this, x => x.WarningsStatusColor, Brushes.Green);

        // Errors status properties
        _errorsStatus = statsObservable
            .Select(stats => stats.Errors > 0 ? "❌" : "✓")
            .ToProperty(this, x => x.ErrorsStatus, "✓");

        _errorsStatusColor = statsObservable
            .Select(stats => stats.Errors > 0 ? Brushes.Red : Brushes.Green)
            .ToProperty(this, x => x.ErrorsStatusColor, Brushes.Green);

        // Button text and icon properties
        _monitoringButtonText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Stop Monitoring" : "Start Monitoring")
            .ToProperty(this, x => x.MonitoringButtonText, "Start Monitoring");

        _monitoringButtonIcon = isMonitoringObservable
            .Select(monitoring => monitoring ? "⏹️" : "▶️")
            .ToProperty(this, x => x.MonitoringButtonIcon, "▶️");

        // Create commands
        var canStart = isMonitoringObservable.Select(x => !x);
        var canStop = isMonitoringObservable;

        StartMonitoringCommand = ReactiveCommand.CreateFromTask(
            StartMonitoringAsync,
            canStart);

        StopMonitoringCommand = ReactiveCommand.CreateFromTask(
            StopMonitoringAsync,
            canStop);

        ToggleMonitoringCommand = ReactiveCommand.CreateFromTask(
            ToggleMonitoringAsync);

        ForceUpdateCommand = ReactiveCommand.CreateFromTask(
            ForceUpdateAsync,
            isMonitoringObservable);

        UpdateLogPathCommand = ReactiveCommand.CreateFromTask<string>(
            UpdateLogPathAsync);

        // Handle command exceptions
        Observable.Merge(
                StartMonitoringCommand.ThrownExceptions,
                StopMonitoringCommand.ThrownExceptions,
                ToggleMonitoringCommand.ThrownExceptions,
                ForceUpdateCommand.ThrownExceptions,
                UpdateLogPathCommand.ThrownExceptions)
            .Subscribe(ex => LastError = ex.Message);
    }

    public PapyrusStats Statistics
    {
        get => _statistics;
        private set => this.RaiseAndSetIfChanged(ref _statistics, value);
    }

    public string? LogFilePath
    {
        get => _logFilePath;
        set => this.RaiseAndSetIfChanged(ref _logFilePath, value);
    }

    public string? LastError
    {
        get => _lastError;
        private set => this.RaiseAndSetIfChanged(ref _lastError, value);
    }

    public bool IsProcessing
    {
        get
        {
            Debug.WriteLine($"IsProcessing requested: {_isProcessing}");
            return _isProcessing;
        }
        private set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    public bool IsMonitoringInternal
    {
        get => _isMonitoringInternal;
        set => this.RaiseAndSetIfChanged(ref _isMonitoringInternal, value);
    }

    public bool EnableAnimations
    {
        get => _enableAnimations;
        set => this.RaiseAndSetIfChanged(ref _enableAnimations, value);
    }

    public bool IsMonitoring
    {
        get => _isMonitoring.Value;
    }

    public string StatusText
    {
        get => _statusText.Value;
    }

    public DateTime LastUpdateTime
    {
        get => _lastUpdateTime.Value;
    }

    public string MonitoringButtonText
    {
        get
        {
            var result = _currentButtonText ?? "Start Monitoring";
            Debug.WriteLine($"MonitoringButtonText requested: '{result}'");
            return result;
        }
    }

    public string MonitoringButtonIcon
    {
        get
        {
            var result = _currentButtonIcon ?? "▶️";
            Debug.WriteLine($"MonitoringButtonIcon requested: '{result}'");
            return result;
        }
    }

    public bool HasStatusMessage
    {
        get => _hasStatusMessage.Value;
    }

    public string StatusMessage
    {
        get => _statusMessage.Value;
    }

    public IBrush StatusMessageBackground
    {
        get => _statusMessageBackground.Value;
    }

    public FontWeight StatusMessageFontWeight
    {
        get => _statusMessageFontWeight.Value;
    }

    public string RatioStatus
    {
        get => _ratioStatus.Value;
    }

    public IBrush RatioStatusColor
    {
        get => _ratioStatusColor.Value;
    }

    public string WarningsStatus
    {
        get => _warningsStatus.Value;
    }

    public IBrush WarningsStatusColor
    {
        get => _warningsStatusColor.Value;
    }

    public string ErrorsStatus
    {
        get => _errorsStatus.Value;
    }

    public IBrush ErrorsStatusColor
    {
        get => _errorsStatusColor.Value;
    }

    public ReactiveCommand<Unit, Unit> StartMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> StopMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceUpdateCommand { get; }
    public ReactiveCommand<string, Unit> UpdateLogPathCommand { get; }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _monitorService?.Dispose();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Subscribe to stats updates from service
        _monitorService.StatsUpdated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stats =>
            {
                Statistics = stats;
                // Record stats in session history
                _sessionHistoryService.RecordStats(stats);
            })
            .DisposeWith(disposables);

        // Subscribe to settings changes
        _settingsService.SettingsChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async settings =>
            {
                LogFilePath = settings.LogFilePath;
                var newConfig = new MonitoringConfiguration
                {
                    LogFilePath = settings.LogFilePath,
                    UpdateIntervalMs = settings.UpdateInterval,
                    UseFileWatcher = true
                };
                await _monitorService.UpdateConfigurationAsync(newConfig);
            })
            .DisposeWith(disposables);

        // Subscribe to errors
        _monitorService.Errors
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(error =>
            {
                LastError = error;
            })
            .DisposeWith(disposables);

        // Clear errors after 5 seconds
        this.WhenAnyValue(x => x.LastError)
            .Where(error => !string.IsNullOrEmpty(error))
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => LastError = null)
            .DisposeWith(disposables);
    }

    private async Task StartMonitoringAsync()
    {
        try
        {
            LastError = null;
            IsProcessing = true;
            IsMonitoringInternal = true;
            _currentButtonText = "Stop Monitoring";
            _currentButtonIcon = "⏹️";
            this.RaisePropertyChanged(nameof(MonitoringButtonText));
            this.RaisePropertyChanged(nameof(MonitoringButtonIcon));
            _cancellationTokenSource = new CancellationTokenSource();

            // Start session history tracking
            _sessionHistoryService.StartSession();

            // Update monitoring configuration from settings
            var config = new MonitoringConfiguration
            {
                LogFilePath = _settingsService.Settings.LogFilePath,
                UpdateIntervalMs = _settingsService.Settings.UpdateInterval,
                UseFileWatcher = true
            };
            await _monitorService.UpdateConfigurationAsync(config);

            // Start the real monitoring service
            await _monitorService.StartAsync(_cancellationTokenSource.Token);

            // Auto-start if configured
            if (_settingsService.Settings.AutoStartMonitoring &&
                !string.IsNullOrEmpty(_settingsService.Settings.LogFilePath))
            {
                // Already starting, so this is covered
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start monitoring: {ex.Message}";
            throw;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            LastError = null;
            IsProcessing = true;
            _cancellationTokenSource?.Cancel();

            // Stop the real monitoring service
            await _monitorService.StopAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

            // End session history tracking
            _sessionHistoryService.EndSession();

            IsMonitoringInternal = false;
            _currentButtonText = "Start Monitoring";
            _currentButtonIcon = "▶️";
            this.RaisePropertyChanged(nameof(MonitoringButtonText));
            this.RaisePropertyChanged(nameof(MonitoringButtonIcon));
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to stop monitoring: {ex.Message}";
            throw;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
        {
            await StopMonitoringAsync();
        }
        else
        {
            await StartMonitoringAsync();
        }
    }

    private async Task ForceUpdateAsync()
    {
        try
        {
            LastError = null;
            // Force update using the real monitoring service
            await _monitorService.ForceUpdateAsync();
        }
        catch (Exception ex)
        {
            LastError = $"Failed to force update: {ex.Message}";
            throw;
        }
    }

    private async Task UpdateLogPathAsync(string path)
    {
        try
        {
            LastError = null;
            LogFilePath = path;

            // Update settings with new path
            var updatedSettings = _settingsService.Settings with { LogFilePath = path };
            await _settingsService.SaveSettingsAsync(updatedSettings);

            // The configuration will be updated via settings change subscription
        }
        catch (Exception ex)
        {
            LastError = $"Failed to update log path: {ex.Message}";
            throw;
        }
    }
}
