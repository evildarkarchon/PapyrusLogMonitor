using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class PapyrusMonitorViewModel : ViewModelBase, IDisposable
{
    private readonly IPapyrusMonitorService _monitorService;
    private readonly ObservableAsPropertyHelper<bool> _isMonitoring;
    private readonly ObservableAsPropertyHelper<string> _statusText;
    private StatisticsViewModel? _statistics;
    private string? _logFilePath;
    private string? _lastError;
    private CancellationTokenSource? _cancellationTokenSource;

    public StatisticsViewModel? Statistics
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

    public bool IsMonitoring => _isMonitoring.Value;
    public string StatusText => _statusText.Value;

    public ReactiveCommand<Unit, Unit> StartMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> StopMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleMonitoringCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceUpdateCommand { get; }
    public ReactiveCommand<string, Unit> UpdateLogPathCommand { get; }

    public PapyrusMonitorViewModel(IPapyrusMonitorService monitorService)
    {
        _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));

        // Initialize statistics view model
        Statistics = new StatisticsViewModel();

        // Create observable for monitoring state
        var isMonitoringObservable = this.WhenAnyValue(x => x._monitorService.IsMonitoring);

        _isMonitoring = isMonitoringObservable
            .ToProperty(this, x => x.IsMonitoring);

        _statusText = isMonitoringObservable
            .Select(monitoring => monitoring ? "Monitoring..." : "Idle")
            .ToProperty(this, x => x.StatusText);

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
        // Subscribe to stats updates
        _monitorService.StatsUpdated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stats =>
            {
                Statistics?.UpdateStats(stats);
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
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Update configuration if path is set
            if (!string.IsNullOrWhiteSpace(LogFilePath))
            {
                var config = new MonitoringConfiguration(LogFilePath);
                await _monitorService.UpdateConfigurationAsync(config, _cancellationTokenSource.Token);
            }

            await _monitorService.StartAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start monitoring: {ex.Message}";
            throw;
        }
    }

    private async Task StopMonitoringAsync()
    {
        try
        {
            LastError = null;
            _cancellationTokenSource?.Cancel();
            await _monitorService.StopAsync();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to stop monitoring: {ex.Message}";
            throw;
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
            await _monitorService.ForceUpdateAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
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

            if (IsMonitoring)
            {
                var config = new MonitoringConfiguration(path);
                await _monitorService.UpdateConfigurationAsync(config, _cancellationTokenSource?.Token ?? CancellationToken.None);
            }
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
        Statistics?.Dispose();
    }
}