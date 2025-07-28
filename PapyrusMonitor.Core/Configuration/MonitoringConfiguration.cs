namespace PapyrusMonitor.Core.Configuration;

/// <summary>
/// Configuration settings for Papyrus log monitoring.
/// 
/// This class contains all the configurable settings that control how the monitoring
/// service operates, including file paths, update intervals, and thresholds.
/// </summary>
public class MonitoringConfiguration
{
    /// <summary>
    /// Gets or sets the path to the Papyrus log file to monitor.
    /// Default is null, which means auto-detection will be attempted.
    /// </summary>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// Gets or sets the interval (in milliseconds) between log file checks.
    /// Default is 1000ms (1 second).
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of log entries to keep in memory.
    /// Used to prevent memory issues with very large log files.
    /// Default is 10,000 entries.
    /// </summary>
    public int MaxLogEntries { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets whether to use FileSystemWatcher for real-time monitoring.
    /// When true, uses FileSystemWatcher for better performance.
    /// When false, polls the file at regular intervals.
    /// Default is true.
    /// </summary>
    public bool UseFileWatcher { get; set; } = true;

    /// <summary>
    /// Gets or sets the ratio threshold for warnings (dumps/stacks).
    /// Ratios above this value will trigger warning status.
    /// Default is 0.5.
    /// </summary>
    public double WarningRatioThreshold { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the ratio threshold for errors (dumps/stacks).
    /// Ratios above this value will trigger error status.
    /// Default is 0.8.
    /// </summary>
    public double ErrorRatioThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets whether to enable automatic encoding detection.
    /// When true, attempts to detect file encoding automatically.
    /// When false, uses UTF-8 encoding.
    /// Default is true.
    /// </summary>
    public bool AutoDetectEncoding { get; set; } = true;

    /// <summary>
    /// Gets or sets the fallback text encoding to use when auto-detection fails.
    /// Default is UTF-8.
    /// </summary>
    public string FallbackEncoding { get; set; } = "UTF-8";

    /// <summary>
    /// Creates a new instance with default configuration values.
    /// </summary>
    public MonitoringConfiguration()
    {
    }

    /// <summary>
    /// Creates a new instance with the specified log file path.
    /// </summary>
    /// <param name="logFilePath">Path to the Papyrus log file</param>
    public MonitoringConfiguration(string logFilePath)
    {
        LogFilePath = logFilePath;
    }

    /// <summary>
    /// Validates the configuration settings and returns any validation errors.
    /// </summary>
    /// <returns>List of validation error messages, empty if configuration is valid</returns>
    public IList<string> Validate()
    {
        var errors = new List<string>();

        if (UpdateIntervalMs <= 0)
            errors.Add("UpdateIntervalMs must be greater than 0");

        if (MaxLogEntries <= 0)
            errors.Add("MaxLogEntries must be greater than 0");

        if (WarningRatioThreshold < 0)
            errors.Add("WarningRatioThreshold must be non-negative");

        if (ErrorRatioThreshold < 0)
            errors.Add("ErrorRatioThreshold must be non-negative");

        if (ErrorRatioThreshold <= WarningRatioThreshold)
            errors.Add("ErrorRatioThreshold must be greater than WarningRatioThreshold");

        if (string.IsNullOrWhiteSpace(FallbackEncoding))
            errors.Add("FallbackEncoding cannot be null or empty");

        return errors;
    }
}