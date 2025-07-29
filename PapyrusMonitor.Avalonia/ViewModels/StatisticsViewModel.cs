using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using PapyrusMonitor.Core.Models;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class StatisticsViewModel : ViewModelBase, IDisposable
{
    private PapyrusStats? _currentStats;
    private string _lastUpdateTime = "Never";
    private readonly ObservableAsPropertyHelper<string> _dumpsDisplay;
    private readonly ObservableAsPropertyHelper<string> _stacksDisplay;
    private readonly ObservableAsPropertyHelper<string> _warningsDisplay;
    private readonly ObservableAsPropertyHelper<string> _errorsDisplay;
    private readonly ObservableAsPropertyHelper<string> _ratioDisplay;
    private readonly ObservableAsPropertyHelper<string> _statusIcon;
    private readonly ObservableAsPropertyHelper<IBrush> _statusColor;
    private readonly ObservableAsPropertyHelper<string> _statusText;

    public PapyrusStats? CurrentStats
    {
        get => _currentStats;
        private set => this.RaiseAndSetIfChanged(ref _currentStats, value);
    }

    public string LastUpdateTime
    {
        get => _lastUpdateTime;
        private set => this.RaiseAndSetIfChanged(ref _lastUpdateTime, value);
    }

    public string DumpsDisplay => _dumpsDisplay.Value;
    public string StacksDisplay => _stacksDisplay.Value;
    public string WarningsDisplay => _warningsDisplay.Value;
    public string ErrorsDisplay => _errorsDisplay.Value;
    public string RatioDisplay => _ratioDisplay.Value;
    public string StatusIcon => _statusIcon.Value;
    public IBrush StatusColor => _statusColor.Value;
    public string StatusText => _statusText.Value;

    public StatisticsViewModel()
    {
        var statsObservable = this.WhenAnyValue(x => x.CurrentStats);

        // Create display properties
        _dumpsDisplay = statsObservable
            .Select(stats => stats?.Dumps.ToString() ?? "0")
            .ToProperty(this, x => x.DumpsDisplay);

        _stacksDisplay = statsObservable
            .Select(stats => stats?.Stacks.ToString() ?? "0")
            .ToProperty(this, x => x.StacksDisplay);

        _warningsDisplay = statsObservable
            .Select(stats => stats?.Warnings.ToString() ?? "0")
            .ToProperty(this, x => x.WarningsDisplay);

        _errorsDisplay = statsObservable
            .Select(stats => stats?.Errors.ToString() ?? "0")
            .ToProperty(this, x => x.ErrorsDisplay);

        _ratioDisplay = statsObservable
            .Select(stats => stats?.Ratio.ToString("F2") ?? "0.00")
            .ToProperty(this, x => x.RatioDisplay);

        // Status calculations based on thresholds
        // TODO: Make these thresholds configurable
        const double warningThreshold = 0.5;
        const double errorThreshold = 0.8;

        var statusObservable = statsObservable
            .Select(stats =>
            {
                if (stats == null) return StatusLevel.None;
                if (stats.Errors > 0 || stats.Ratio >= errorThreshold) return StatusLevel.Error;
                if (stats.Warnings > 0 || stats.Ratio >= warningThreshold) return StatusLevel.Warning;
                return StatusLevel.Good;
            });

        _statusIcon = statusObservable
            .Select(status => status switch
            {
                StatusLevel.Good => "✓",
                StatusLevel.Warning => "⚠️",
                StatusLevel.Error => "❌",
                _ => "⏸"
            })
            .ToProperty(this, x => x.StatusIcon);

        _statusColor = statusObservable
            .Select(status => status switch
            {
                StatusLevel.Good => Brushes.Green,
                StatusLevel.Warning => Brushes.Orange,
                StatusLevel.Error => Brushes.Red,
                _ => Brushes.Gray
            })
            .ToProperty(this, x => x.StatusColor);

        _statusText = statusObservable
            .Select(status => status switch
            {
                StatusLevel.Good => "Healthy",
                StatusLevel.Warning => "Warning",
                StatusLevel.Error => "Critical",
                _ => "No Data"
            })
            .ToProperty(this, x => x.StatusText);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Update time display every second when stats are available
        Observable.Interval(TimeSpan.FromSeconds(1))
            .Where(_ => CurrentStats != null)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateTimeDisplay())
            .DisposeWith(disposables);
    }

    public void UpdateStats(PapyrusStats stats)
    {
        CurrentStats = stats;
        UpdateTimeDisplay();
    }

    private void UpdateTimeDisplay()
    {
        if (CurrentStats == null)
        {
            LastUpdateTime = "Never";
            return;
        }

        var elapsed = DateTime.Now - CurrentStats.Timestamp;
        
        LastUpdateTime = elapsed.TotalSeconds switch
        {
            < 1 => "Just now",
            < 60 => $"{(int)elapsed.TotalSeconds} seconds ago",
            < 3600 => $"{(int)elapsed.TotalMinutes} minutes ago",
            _ => CurrentStats.Timestamp.ToString("HH:mm:ss")
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    private enum StatusLevel
    {
        None,
        Good,
        Warning,
        Error
    }
}