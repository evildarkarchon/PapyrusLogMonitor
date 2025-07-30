using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
///     Interface for parsing Papyrus log files and extracting statistics.
///     This interface defines the contract for log parsing implementations that can
///     read Papyrus log files, parse individual lines, and aggregate statistics.
/// </summary>
public interface ILogParser
{
    /// <summary>
    ///     Parses a single log line and returns the corresponding log entry.
    /// </summary>
    /// <param name="line">The log line to parse</param>
    /// <param name="lineNumber">Optional line number in the source file</param>
    /// <returns>A LogEntry representing the parsed line</returns>
    LogEntry ParseLine(string line, int? lineNumber = null);

    /// <summary>
    ///     Parses multiple log lines and returns a collection of log entries.
    /// </summary>
    /// <param name="lines">The log lines to parse</param>
    /// <returns>A collection of LogEntry objects</returns>
    IEnumerable<LogEntry> ParseLines(IEnumerable<string> lines);

    /// <summary>
    ///     Reads and parses an entire log file, returning aggregated statistics.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>PapyrusStats containing aggregated statistics from the file</returns>
    Task<PapyrusStats> ParseFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Aggregates a collection of log entries into PapyrusStats.
    /// </summary>
    /// <param name="entries">The log entries to aggregate</param>
    /// <returns>PapyrusStats containing the aggregated statistics</returns>
    PapyrusStats AggregateStats(IEnumerable<LogEntry> entries);

    /// <summary>
    ///     Detects the text encoding of a log file.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The detected encoding name, or null if detection fails</returns>
    Task<string?> DetectEncodingAsync(string filePath, CancellationToken cancellationToken = default);
}
