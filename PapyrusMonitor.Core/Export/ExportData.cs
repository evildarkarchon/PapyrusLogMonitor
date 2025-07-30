using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Export;

/// <summary>
/// Data model for exporting monitoring statistics
/// </summary>
public record ExportData
{
    /// <summary>
    /// Export metadata
    /// </summary>
    public ExportMetadata Metadata { get; init; } = new();
    
    /// <summary>
    /// Collection of statistics to export
    /// </summary>
    public IReadOnlyList<PapyrusStats> Statistics { get; init; } = Array.Empty<PapyrusStats>();
    
    /// <summary>
    /// Session summary statistics
    /// </summary>
    public SessionSummary? Summary { get; init; }
}

/// <summary>
/// Metadata about the export
/// </summary>
public record ExportMetadata
{
    /// <summary>
    /// Date and time of export
    /// </summary>
    public DateTime ExportDate { get; init; } = DateTime.Now;
    
    /// <summary>
    /// Version of the application
    /// </summary>
    public string ApplicationVersion { get; init; } = "1.0.0";
    
    /// <summary>
    /// Path to the monitored log file
    /// </summary>
    public string LogFilePath { get; init; } = string.Empty;
    
    /// <summary>
    /// Start time of the monitoring session
    /// </summary>
    public DateTime? SessionStartTime { get; init; }
    
    /// <summary>
    /// End time of the monitoring session
    /// </summary>
    public DateTime? SessionEndTime { get; init; }
}

/// <summary>
/// Summary statistics for a monitoring session
/// </summary>
public record SessionSummary
{
    /// <summary>
    /// Total number of dumps during session
    /// </summary>
    public int TotalDumps { get; init; }
    
    /// <summary>
    /// Total number of stacks during session
    /// </summary>
    public int TotalStacks { get; init; }
    
    /// <summary>
    /// Total number of warnings during session
    /// </summary>
    public int TotalWarnings { get; init; }
    
    /// <summary>
    /// Total number of errors during session
    /// </summary>
    public int TotalErrors { get; init; }
    
    /// <summary>
    /// Average ratio during session
    /// </summary>
    public double AverageRatio { get; init; }
    
    /// <summary>
    /// Peak dumps value during session
    /// </summary>
    public int PeakDumps { get; init; }
    
    /// <summary>
    /// Peak stacks value during session
    /// </summary>
    public int PeakStacks { get; init; }
    
    /// <summary>
    /// Duration of the monitoring session
    /// </summary>
    public TimeSpan Duration { get; init; }
}