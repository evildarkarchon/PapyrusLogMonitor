using System.Text.Json.Serialization;

namespace PapyrusMonitor.Core.Configuration;

/// <summary>
///     Application settings model for configuration persistence
/// </summary>
public record AppSettings
{
    /// <summary>
    ///     Path to the Papyrus log file
    /// </summary>
    [JsonPropertyName("logFilePath")]
    public string LogFilePath { get; init; } = string.Empty;

    /// <summary>
    ///     Update interval in milliseconds for monitoring
    /// </summary>
    [JsonPropertyName("updateInterval")]
    public int UpdateInterval { get; init; } = 1000;

    /// <summary>
    ///     Whether to start monitoring automatically on application startup
    /// </summary>
    [JsonPropertyName("autoStartMonitoring")]
    public bool AutoStartMonitoring { get; init; }

    /// <summary>
    ///     Maximum number of log entries to keep in memory
    /// </summary>
    [JsonPropertyName("maxLogEntries")]
    public int MaxLogEntries { get; init; } = 10000;

    /// <summary>
    ///     Whether to show notifications for errors
    /// </summary>
    [JsonPropertyName("showErrorNotifications")]
    public bool ShowErrorNotifications { get; init; } = true;

    /// <summary>
    ///     Whether to show notifications for warnings
    /// </summary>
    [JsonPropertyName("showWarningNotifications")]
    public bool ShowWarningNotifications { get; init; }

    /// <summary>
    ///     Export settings
    /// </summary>
    [JsonPropertyName("exportSettings")]
    public ExportSettings ExportSettings { get; init; } = new();

    /// <summary>
    ///     Window state settings
    /// </summary>
    [JsonPropertyName("windowSettings")]
    public WindowSettings WindowSettings { get; init; } = new();
}

/// <summary>
///     Export-related settings
/// </summary>
public record ExportSettings
{
    /// <summary>
    ///     Default export directory
    /// </summary>
    [JsonPropertyName("defaultExportPath")]
    public string DefaultExportPath { get; init; } = string.Empty;

    /// <summary>
    ///     Whether to include timestamps in exports
    /// </summary>
    [JsonPropertyName("includeTimestamps")]
    public bool IncludeTimestamps { get; init; } = true;

    /// <summary>
    ///     Date format for exports
    /// </summary>
    [JsonPropertyName("dateFormat")]
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
}

/// <summary>
///     Window state settings
/// </summary>
public record WindowSettings
{
    /// <summary>
    ///     Window X position
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; init; } = -1;

    /// <summary>
    ///     Window Y position
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; init; } = -1;

    /// <summary>
    ///     Window width
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; init; } = 800;

    /// <summary>
    ///     Window height
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; init; } = 600;

    /// <summary>
    ///     Whether the window is maximized
    /// </summary>
    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; init; }
}
