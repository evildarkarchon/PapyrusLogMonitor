using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class PapyrusMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IPapyrusMonitorService _monitorService;
    private readonly ObservableAsPropertyHelper<bool> _isMonitoring;
    private readonly ObservableAsPropertyHelper<string> _statusText;
    private readonly ObservableAsPropertyHelper<DateTime> _lastUpdateTime;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonText;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonIcon;
    private string _currentButtonText = "Start Monitoring";
    private string _currentButtonIcon = "▶️";
    private readonly ObservableAsPropertyHelper<bool> _hasStatusMessage;
    private readonly ObservableAsPropertyHelper<string> _statusMessage;
    private readonly ObservableAsPropertyHelper<IBrush> _statusMessageBackground;
    private readonly ObservableAsPropertyHelper<FontWeight> _statusMessageFontWeight;
    private readonly ObservableAsPropertyHelper<string> _ratioStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _ratioStatusColor;
    private readonly ObservableAsPropertyHelper<string> _warningsStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _warningsStatusColor;
    private readonly ObservableAsPropertyHelper<string> _errorsStatus;
    private readonly ObservableAsPropertyHelper<IBrush> _errorsStatusColor;
    private PapyrusStats _statistics = new(DateTime.Now, 0, 0, 0, 0, 0.0);
    private string? _logFilePath;
    private string? _lastError;
    private bool _isProcessing;
    private bool _isMonitoringInternal;
    private bool _enableAnimations = true;
    private CancellationTokenSource? _cancellationTokenSource;

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
            System.Diagnostics.Debug.WriteLine($"IsProcessing requested: {_isProcessing}");
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

    public bool IsMonitoring => _isMonitoring.Value;
    public string StatusText => _statusText.Value;
    public DateTime LastUpdateTime => _lastUpdateTime.Value;
    public string MonitoringButtonText 
    { 
        get 
        {
            var result = _currentButtonText ?? "Start Monitoring";
            System.Diagnostics.Debug.WriteLine($"MonitoringButtonText requested: '{result}'");
            return result;
        }
    }
    public string MonitoringButtonIcon 
    { 
        get 
        {
            var result = _currentButtonIcon ?? "▶️";
            System.Diagnostics.Debug.WriteLine($"MonitoringButtonIcon requested: '{result}'");
            return result;
        }
    }
    public bool HasStatusMessage => _hasStatusMessage.Value;
    public string StatusMessage => _statusMessage.Value;
    public IBrush StatusMessageBackground => _statusMessageBackground.Value;
    public FontWeight StatusMessageFontWeight => _statusMessageFontWeight.Value;
    public string RatioStatus => _ratioStatus.Value;
    public IBrush RatioStatusColor => _ratioStatusColor.Value;
    public string WarningsStatus => _warningsStatus.Value;
    public IBrush WarningsStatusColor => _warningsStatusColor.Value;
    public string ErrorsStatus => _errorsStatus.Value;
    public IBrush ErrorsStatusColor => _errorsStatusColor.Value;

    public ReactiveCommand<Unit, Unit> StartMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> StopMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceUpdateCommand { get; }
    public ReactiveCommand<string, Unit> UpdateLogPathCommand { get; }

    public PapyrusMonitorViewModel(IPapyrusMonitorService monitorService, MonitoringConfiguration configuration)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        
        // Set initial log path from configuration
        LogFilePath = configuration?.LogFilePath;

        // Create observable for monitoring state
        var isMonitoringObservable = this.WhenAnyValue(x => x.IsMonitoringInternal)
            .StartWith(IsMonitoringInternal); // Ensure initial value is emitted

        _isMonitoring = isMonitoringObservable
            .ToProperty(this, x => x.IsMonitoring, initialValue: false);

        _statusText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Monitoring..." : "Idle")
            .ToProperty(this, x => x.StatusText, initialValue: "Idle");

        // Last update time
        _lastUpdateTime = this.WhenAnyValue(x => x.Statistics)
            .Select(stats => stats?.Timestamp ?? DateTime.Now)
            .ToProperty(this, x => x.LastUpdateTime, initialValue: DateTime.Now);

        // Monitoring button properties
        _monitoringButtonText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Stop Monitoring" : "Start Monitoring")
            .ToProperty(this, x => x.MonitoringButtonText, initialValue: "Start Monitoring");

        _monitoringButtonIcon = isMonitoringObservable
            .Select(monitoring => monitoring ? "⏹️" : "▶️")
            .ToProperty(this, x => x.MonitoringButtonIcon, initialValue: "▶️");

        // Status message properties based on statistics
        var statsObservable = this.WhenAnyValue(x => x.Statistics);

        _hasStatusMessage = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) => !string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0 || stats.Ratio > 0.5)
            .ToProperty(this, x => x.HasStatusMessage, initialValue: false);

        _statusMessage = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                        return $"Error: {error}";
                    if (stats.Errors > 0)
                        return $"{stats.Errors} errors detected in Papyrus log!";
                    if (stats.Warnings > 0)
                        return $"{stats.Warnings} warnings detected in Papyrus log.";
                    if (stats.Ratio > 0.8)
                        return "Warning: High dumps-to-stacks ratio detected!";
                    if (stats.Ratio > 0.5)
                        return "Caution: Elevated dumps-to-stacks ratio.";
                    return "Papyrus log appears normal.";
                })
            .ToProperty(this, x => x.StatusMessage, initialValue: "Papyrus log appears normal.");

        _statusMessageBackground = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) =>
                {
                    if (!string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Ratio > 0.8)
                        return new SolidColorBrush(Color.FromRgb(255, 230, 230)); // Light red
                    if (stats.Warnings > 0 || stats.Ratio > 0.5)
                        return new SolidColorBrush(Color.FromRgb(255, 244, 229)); // Light orange
                    return new SolidColorBrush(Color.FromRgb(230, 255, 230)); // Light green
                })
            .ToProperty(this, x => x.StatusMessageBackground, initialValue: new SolidColorBrush(Color.FromRgb(230, 255, 230)));

        _statusMessageFontWeight = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) => (!string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0) 
                    ? FontWeight.Bold 
                    : FontWeight.Normal)
            .ToProperty(this, x => x.StatusMessageFontWeight, initialValue: FontWeight.Normal);

        // Ratio status properties
        _ratioStatus = statsObservable
            .Select(stats => stats.Ratio > 0.8 ? "❌" : stats.Ratio > 0.5 ? "⚠️" : "✓")
            .ToProperty(this, x => x.RatioStatus, initialValue: "✓");

        _ratioStatusColor = statsObservable
            .Select(stats => stats.Ratio > 0.8 
                ? Brushes.Red 
                : stats.Ratio > 0.5 
                    ? Brushes.Orange 
                    : Brushes.Green)
            .ToProperty(this, x => x.RatioStatusColor, initialValue: Brushes.Green);

        // Warnings status properties
        _warningsStatus = statsObservable
            .Select(stats => stats.Warnings > 0 ? "⚠️" : "✓")
            .ToProperty(this, x => x.WarningsStatus, initialValue: "✓");

        _warningsStatusColor = statsObservable
            .Select(stats => stats.Warnings > 0 ? Brushes.Orange : Brushes.Green)
            .ToProperty(this, x => x.WarningsStatusColor, initialValue: Brushes.Green);

        // Errors status properties
        _errorsStatus = statsObservable
            .Select(stats => stats.Errors > 0 ? "❌" : "✓")
            .ToProperty(this, x => x.ErrorsStatus, initialValue: "✓");

        _errorsStatusColor = statsObservable
            .Select(stats => stats.Errors > 0 ? Brushes.Red : Brushes.Green)
            .ToProperty(this, x => x.ErrorsStatusColor, initialValue: Brushes.Green);

        // Button text and icon properties
        _monitoringButtonText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Stop Monitoring" : "Start Monitoring")
            .ToProperty(this, x => x.MonitoringButtonText, initialValue: "Start Monitoring");

        _monitoringButtonIcon = isMonitoringObservable
            .Select(monitoring => monitoring ? "⏹️" : "▶️")
            .ToProperty(this, x => x.MonitoringButtonIcon, initialValue: "▶️");

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

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Subscribe to stats updates from service
        _monitorService.StatsUpdated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stats =>
            {
                Statistics = stats;
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
            
            // Start the real monitoring service
            await _monitorService.StartAsync(_cancellationTokenSource.Token);
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
            
            // Update the service configuration with new path
            var newConfig = new MonitoringConfiguration
            {
                LogFilePath = path,
                UpdateIntervalMs = _monitorService.Configuration.UpdateIntervalMs,
                UseFileWatcher = _monitorService.Configuration.UseFileWatcher
            };
            await _monitorService.UpdateConfigurationAsync(newConfig);
        }
        catch (Exception ex)
        {
            LastError = $"Failed to update log path: {ex.Message}";
            throw;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _monitorService?.Dispose();
    }
}