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
    private readonly IPapyrusMonitorService? _monitorService;
    private readonly ObservableAsPropertyHelper<bool> _isMonitoring;
    private readonly ObservableAsPropertyHelper<string> _statusText;
    private readonly ObservableAsPropertyHelper<DateTime> _lastUpdateTime;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonText;
    private readonly ObservableAsPropertyHelper<string> _monitoringButtonIcon;
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
        get => _isProcessing;
        private set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    private bool IsMonitoringInternal
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
    public string MonitoringButtonText => _monitoringButtonText.Value;
    public string MonitoringButtonIcon => _monitoringButtonIcon.Value;
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

    public PapyrusMonitorViewModel()
    {
        // For now, create service manually - in production this would come from DI
        // Set a default log path for testing
        LogFilePath = @"C:\Users\<username>\Documents\My Games\Fallout4\Logs\Script\Papyrus.0.log";

        // Create observable for monitoring state
        var isMonitoringObservable = this.WhenAnyValue(x => x.IsMonitoringInternal);

        _isMonitoring = isMonitoringObservable
            .ToProperty(this, x => x.IsMonitoring);

        _statusText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Monitoring..." : "Idle")
            .ToProperty(this, x => x.StatusText);

        // Last update time
        _lastUpdateTime = this.WhenAnyValue(x => x.Statistics)
            .Select(stats => stats?.Timestamp ?? DateTime.Now)
            .ToProperty(this, x => x.LastUpdateTime);

        // Monitoring button properties
        _monitoringButtonText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Stop Monitoring" : "Start Monitoring")
            .ToProperty(this, x => x.MonitoringButtonText);

        _monitoringButtonIcon = isMonitoringObservable
            .Select(monitoring => monitoring ? "⏹️" : "▶️")
            .ToProperty(this, x => x.MonitoringButtonIcon);

        // Status message properties based on statistics
        var statsObservable = this.WhenAnyValue(x => x.Statistics);

        _hasStatusMessage = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) => !string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0 || stats.Ratio > 0.5)
            .ToProperty(this, x => x.HasStatusMessage);

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
            .ToProperty(this, x => x.StatusMessage);

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
            .ToProperty(this, x => x.StatusMessageBackground);

        _statusMessageFontWeight = Observable.CombineLatest(
                statsObservable,
                this.WhenAnyValue(x => x.LastError),
                (stats, error) => (!string.IsNullOrEmpty(error) || stats.Errors > 0 || stats.Warnings > 0) 
                    ? FontWeight.Bold 
                    : FontWeight.Normal)
            .ToProperty(this, x => x.StatusMessageFontWeight);

        // Ratio status properties
        _ratioStatus = statsObservable
            .Select(stats => stats.Ratio > 0.8 ? "❌" : stats.Ratio > 0.5 ? "⚠️" : "✓")
            .ToProperty(this, x => x.RatioStatus);

        _ratioStatusColor = statsObservable
            .Select(stats => stats.Ratio > 0.8 
                ? Brushes.Red 
                : stats.Ratio > 0.5 
                    ? Brushes.Orange 
                    : Brushes.Green)
            .ToProperty(this, x => x.RatioStatusColor);

        // Warnings status properties
        _warningsStatus = statsObservable
            .Select(stats => stats.Warnings > 0 ? "⚠️" : "✓")
            .ToProperty(this, x => x.WarningsStatus);

        _warningsStatusColor = statsObservable
            .Select(stats => stats.Warnings > 0 ? Brushes.Orange : Brushes.Green)
            .ToProperty(this, x => x.WarningsStatusColor);

        // Errors status properties
        _errorsStatus = statsObservable
            .Select(stats => stats.Errors > 0 ? "❌" : "✓")
            .ToProperty(this, x => x.ErrorsStatus);

        _errorsStatusColor = statsObservable
            .Select(stats => stats.Errors > 0 ? Brushes.Red : Brushes.Green)
            .ToProperty(this, x => x.ErrorsStatusColor);

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
        // Subscribe to stats updates if service is available
        if (_monitorService != null)
        {
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
        }

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
            _cancellationTokenSource = new CancellationTokenSource();
            
            // For now, just simulate monitoring with dummy data
            await Task.Run(async () =>
            {
                var random = new Random();
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    
                    // Simulate stats updates
                    var dumps = Statistics.Dumps + random.Next(0, 2);
                    var stacks = Statistics.Stacks + random.Next(0, 3);
                    var warnings = Statistics.Warnings + (random.Next(0, 100) > 95 ? 1 : 0);
                    var errors = Statistics.Errors + (random.Next(0, 100) > 98 ? 1 : 0);
                    var ratio = stacks > 0 ? (double)dumps / stacks : 0.0;
                    
                    Statistics = new PapyrusStats(
                        DateTime.Now,
                        dumps,
                        stacks,
                        warnings,
                        errors,
                        ratio);
                }
            }, _cancellationTokenSource.Token);
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
            await Task.Delay(100); // Small delay to ensure cancellation is processed
            IsMonitoringInternal = false;
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
            // Simulate force update
            await Task.Delay(100);
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
            await Task.CompletedTask;
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