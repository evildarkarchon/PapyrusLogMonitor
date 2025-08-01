using System.Reactive.Linq;
using System.Reactive.Subjects;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Services;

/// <summary>
///     Implementation of IPapyrusMonitorService that provides real-time monitoring of Papyrus logs.
///     This service coordinates file watching, tail reading, and log parsing to provide
///     observable streams of statistics updates and errors. It supports both file-watcher
///     based monitoring and timer-based polling.
/// </summary>
public class PapyrusMonitorService : IPapyrusMonitorService
{
    private readonly Subject<string> _errorSubject;
    private readonly IFileWatcher _fileWatcher;
    private readonly object _lock = new();
    private readonly ILogParser _logParser;
    private readonly Subject<PapyrusStats> _statsSubject;
    private readonly IFileTailReader _tailReader;

    private bool _disposed;
    private bool _isMonitoring;
    private CancellationTokenSource? _monitoringCancellation;
    private Timer? _pollingTimer;

    /// <summary>
    ///     Initializes a new instance of the PapyrusMonitorService class.
    /// </summary>
    /// <param name="logParser">The log parser for processing log content</param>
    /// <param name="fileWatcher">The file watcher for detecting file changes</param>
    /// <param name="tailReader">The tail reader for efficient file reading</param>
    /// <param name="configuration">The monitoring configuration</param>
    public PapyrusMonitorService(
        ILogParser logParser,
        IFileWatcher fileWatcher,
        IFileTailReader tailReader,
        MonitoringConfiguration configuration)
    {
        _logParser = logParser ?? throw new ArgumentNullException(nameof(logParser));
        _fileWatcher = fileWatcher ?? throw new ArgumentNullException(nameof(fileWatcher));
        _tailReader = tailReader ?? throw new ArgumentNullException(nameof(tailReader));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _statsSubject = new Subject<PapyrusStats>();
        _errorSubject = new Subject<string>();

        // Subscribe to file watcher events
        _fileWatcher.FileChanged
            .Where(_ => _isMonitoring)
            .Subscribe(async void (_) =>
            {
                try
                {
                    await ProcessFileChangeAsync();
                }
                catch (Exception)
                {
                    // TODO handle exception
                }
            }, HandleError);

        _fileWatcher.Errors
            .Subscribe(_errorSubject.OnNext, HandleError);
    }

    /// <summary>
    ///     Gets an observable stream of PapyrusStats updates.
    /// </summary>
    public IObservable<PapyrusStats> StatsUpdated
    {
        get => _statsSubject.AsObservable();
    }

    /// <summary>
    ///     Gets an observable stream of error messages.
    /// </summary>
    public IObservable<string> Errors
    {
        get => _errorSubject.AsObservable();
    }

    /// <summary>
    ///     Gets a value indicating whether the service is currently monitoring.
    /// </summary>
    public bool IsMonitoring
    {
        get
        {
            lock (_lock)
            {
                return _isMonitoring;
            }
        }
    }

    /// <summary>
    ///     Gets the current monitoring configuration.
    /// </summary>
    public MonitoringConfiguration Configuration { get; private set; }

    /// <summary>
    ///     Gets the most recent statistics.
    /// </summary>
    public PapyrusStats? LastStats { get; private set; }

    /// <summary>
    ///     Starts monitoring the configured log file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the monitoring operation</param>
    /// <returns>A task that completes when monitoring has started</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PapyrusMonitorService));
        }

        lock (_lock)
        {
            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
        }

        try
        {
            // Validate configuration
            var validationErrors = Configuration.Validate();
            if (validationErrors.Any())
            {
                var errorMessage = string.Join("; ", validationErrors);
                _errorSubject.OnNext($"Configuration validation failed: {errorMessage}");
                return;
            }

            // Check if log file path is configured
            if (string.IsNullOrWhiteSpace(Configuration.LogFilePath))
            {
                _errorSubject.OnNext("Log file path is not configured");
                return;
            }

            _monitoringCancellation = new CancellationTokenSource();

            // Initialize tail reader
            await _tailReader.InitializeAsync(Configuration.LogFilePath, false, cancellationToken);

            // Start file watching or polling based on configuration
            if (Configuration.UseFileWatcher)
            {
                await _fileWatcher.StartWatchingAsync(Configuration.LogFilePath, cancellationToken);
            }
            else
            {
                StartPollingTimer();
            }

            // Do initial stats collection
            await ForceUpdateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _isMonitoring = false;
            }

            _errorSubject.OnNext($"Failed to start monitoring: {ex.Message}");
        }
    }

    /// <summary>
    ///     Stops monitoring the log file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the stop operation</param>
    /// <returns>A task that completes when monitoring has stopped</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isMonitoring)
            {
                return;
            }

            _isMonitoring = false;
        }

        try
        {
            // Cancel monitoring operations
            _monitoringCancellation?.Cancel();

            // Stop file watcher
            if (_fileWatcher.IsWatching)
            {
                await _fileWatcher.StopWatchingAsync(cancellationToken);
            }

            // Stop polling timer
            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _monitoringCancellation?.Dispose();
            _monitoringCancellation = null;
        }
        catch (Exception ex)
        {
            _errorSubject.OnNext($"Error during stop: {ex.Message}");
        }
    }

    /// <summary>
    ///     Updates the monitoring configuration.
    /// </summary>
    /// <param name="configuration">The new configuration to use</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the configuration has been updated</returns>
    public async Task UpdateConfigurationAsync(MonitoringConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var wasMonitoring = IsMonitoring;

        if (wasMonitoring)
        {
            await StopAsync(cancellationToken);
        }

        Configuration = configuration;

        if (wasMonitoring)
        {
            await StartAsync(cancellationToken);
        }
    }

    /// <summary>
    ///     Forces an immediate update of statistics.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the update is finished</returns>
    public async Task ForceUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Configuration.LogFilePath))
        {
            return;
        }

        try
        {
            // Parse the entire file to get current stats
            var stats = await _logParser.ParseFileAsync(Configuration.LogFilePath, cancellationToken);

            // Only emit if stats have changed
            if (LastStats == null || !LastStats.Equals(stats))
            {
                LastStats = stats;
                _statsSubject.OnNext(stats);
            }
        }
        catch (Exception ex)
        {
            _errorSubject.OnNext($"Error during force update: {ex.Message}");
        }
    }

    /// <summary>
    ///     Disposes the monitoring service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var stopTask = StopAsync();
        try
        {
            stopTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore timeout/exceptions during dispose
        }

        _fileWatcher.Dispose();
        _tailReader.Dispose();
        _statsSubject.Dispose();
        _errorSubject.Dispose();
        _monitoringCancellation?.Dispose();
        _pollingTimer?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task ProcessFileChangeAsync()
    {
        var cancellation = _monitoringCancellation;
        if (!_isMonitoring || _disposed || cancellation?.Token.IsCancellationRequested == true)
        {
            return;
        }

        try
        {
            var token = cancellation?.Token ?? CancellationToken.None;

            // Check if there's new content to read
            if (!await _tailReader.HasNewContentAsync(token))
            {
                return;
            }

            // Read new lines
            var newLines = await _tailReader.ReadNewLinesAsync(token);

            var enumerable = newLines as string[] ?? newLines.ToArray();
            if (enumerable.Any())
            {
                // Parse the new lines and update running totals
                var entries = _logParser.ParseLines(enumerable);
                var newStats = _logParser.AggregateStats(entries);

                // Combine with existing stats if we have them
                if (LastStats != null)
                {
                    var combinedStats = new PapyrusStats(
                        DateTime.Now,
                        LastStats.Dumps + newStats.Dumps,
                        LastStats.Stacks + newStats.Stacks,
                        LastStats.Warnings + newStats.Warnings,
                        LastStats.Errors + newStats.Errors,
                        0.0 // Will be recalculated
                    );

                    // Recalculate ratio
                    var ratio = combinedStats.Stacks == 0 ? 0.0 : (double)combinedStats.Dumps / combinedStats.Stacks;
                    combinedStats = combinedStats with { Ratio = ratio };

                    // Only emit if different from last stats
                    if (!LastStats.Equals(combinedStats))
                    {
                        LastStats = combinedStats;

                        // Check if not disposed before emitting
                        if (!_disposed)
                        {
                            _statsSubject.OnNext(combinedStats);
                        }
                    }
                }
                else
                {
                    LastStats = newStats;

                    // Check if not disposed before emitting
                    if (!_disposed)
                    {
                        _statsSubject.OnNext(newStats);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Check if not disposed before emitting error
            if (!_disposed)
            {
                _errorSubject.OnNext($"Error processing file change: {ex.Message}");
            }
        }
    }

    private void StartPollingTimer()
    {
        _pollingTimer = new Timer(async void (_) =>
            {
                try
                {
                    await ProcessFileChangeAsync();
                }
                catch (Exception)
                {
                    // TODO handle exception
                }
            },
            null, TimeSpan.Zero, TimeSpan.FromMilliseconds(Configuration.UpdateIntervalMs));
    }

    private void HandleError(Exception ex)
    {
        if (!_disposed)
        {
            _errorSubject.OnNext($"Internal error: {ex.Message}");
        }
    }
}
