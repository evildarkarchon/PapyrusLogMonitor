using System.Reactive;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
/// Interface for the Papyrus log monitoring service.
/// 
/// This service provides real-time monitoring of Papyrus log files, emitting observable
/// streams of statistics updates and errors. It supports configurable monitoring
/// intervals and file watching strategies.
/// </summary>
public interface IPapyrusMonitorService : IDisposable
{
    /// <summary>
    /// Gets an observable stream of PapyrusStats updates.
    /// Updates are emitted only when statistics change from the previous values.
    /// </summary>
    IObservable<PapyrusStats> StatsUpdated { get; }

    /// <summary>
    /// Gets an observable stream of error messages that occur during monitoring.
    /// </summary>
    IObservable<string> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the service is currently monitoring.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Gets the current monitoring configuration.
    /// </summary>
    MonitoringConfiguration Configuration { get; }

    /// <summary>
    /// Gets the most recent statistics, or null if no stats have been collected yet.
    /// </summary>
    PapyrusStats? LastStats { get; }

    /// <summary>
    /// Starts monitoring the configured log file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the monitoring operation</param>
    /// <returns>A task that completes when monitoring has started</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring the log file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the stop operation</param>
    /// <returns>A task that completes when monitoring has stopped</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the monitoring configuration. If monitoring is active, it will be
    /// restarted with the new configuration.
    /// </summary>
    /// <param name="configuration">The new configuration to use</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the configuration has been updated</returns>
    Task UpdateConfigurationAsync(MonitoringConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces an immediate update of statistics by re-reading the log file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the update is finished</returns>
    Task ForceUpdateAsync(CancellationToken cancellationToken = default);
}