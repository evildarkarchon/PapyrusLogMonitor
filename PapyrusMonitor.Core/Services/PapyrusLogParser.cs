using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Services;

/// <summary>
/// Implementation of ILogParser for parsing Papyrus log files.
/// 
/// This class provides concrete implementations for parsing Papyrus log files,
/// extracting statistics, and handling various encoding scenarios.
/// </summary>
public partial class PapyrusLogParser : ILogParser
{
    private readonly IFileSystem _fileSystem;

    // Compiled regex patterns for better performance
    [GeneratedRegex(@"Dumping Stacks", RegexOptions.Compiled)]
    private static partial Regex DumpingStacksRegex();

    [GeneratedRegex(@"Dumping Stack", RegexOptions.Compiled)]
    private static partial Regex DumpingStackRegex();

    [GeneratedRegex(@" warning: ", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex WarningRegex();

    [GeneratedRegex(@" error: ", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    /// <summary>
    /// Initializes a new instance of the PapyrusLogParser class.
    /// </summary>
    /// <param name="fileSystem">File system abstraction for testability</param>
    public PapyrusLogParser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Parses a single log line and determines its type and content.
    /// </summary>
    /// <param name="line">The log line to parse</param>
    /// <param name="lineNumber">Optional line number in the source file</param>
    /// <returns>A LogEntry representing the parsed line</returns>
    public LogEntry ParseLine(string line, int? lineNumber = null)
    {
        if (string.IsNullOrEmpty(line))
        {
            return new LogEntry(DateTime.Now, line ?? string.Empty, LogEntryType.Unknown, lineNumber);
        }

        var entryType = DetermineLogEntryType(line);
        return new LogEntry(DateTime.Now, line, entryType, lineNumber);
    }

    /// <summary>
    /// Parses multiple log lines and returns a collection of log entries.
    /// </summary>
    /// <param name="lines">The log lines to parse</param>
    /// <returns>A collection of LogEntry objects</returns>
    public IEnumerable<LogEntry> ParseLines(IEnumerable<string> lines)
    {
        var lineNumber = 1;
        foreach (var line in lines)
        {
            yield return ParseLine(line, lineNumber++);
        }
    }

    /// <summary>
    /// Reads and parses an entire log file, returning aggregated statistics.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>PapyrusStats containing aggregated statistics from the file</returns>
    public async Task<PapyrusStats> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!_fileSystem.File.Exists(filePath))
        {
            // Return empty stats if file doesn't exist
            return new PapyrusStats(DateTime.Now, 0, 0, 0, 0, 0.0);
        }

        try
        {
            // Detect encoding
            var encoding = await DetectEncodingAsync(filePath, cancellationToken);
            var encodingToUse = encoding != null ? Encoding.GetEncoding(encoding) : Encoding.UTF8;

            // Read file with proper encoding and sharing options
            var lines = await _fileSystem.File.ReadAllLinesAsync(filePath, encodingToUse, cancellationToken);
            
            // Parse all lines and aggregate stats
            var entries = ParseLines(lines);
            return AggregateStats(entries);
        }
        catch (UnauthorizedAccessException)
        {
            // File might be locked by the game, return previous stats or empty
            return new PapyrusStats(DateTime.Now, 0, 0, 0, 0, 0.0);
        }
        catch (IOException)
        {
            // File I/O issues, return empty stats
            return new PapyrusStats(DateTime.Now, 0, 0, 0, 0, 0.0);
        }
    }

    /// <summary>
    /// Aggregates a collection of log entries into PapyrusStats.
    /// </summary>
    /// <param name="entries">The log entries to aggregate</param>
    /// <returns>PapyrusStats containing the aggregated statistics</returns>
    public PapyrusStats AggregateStats(IEnumerable<LogEntry> entries)
    {
        var dumps = 0;
        var stacks = 0;
        var warnings = 0;
        var errors = 0;

        foreach (var entry in entries)
        {
            switch (entry.Type)
            {
                case LogEntryType.DumpingStacks:
                    dumps++;
                    break;
                case LogEntryType.DumpingStack:
                    stacks++;
                    break;
                case LogEntryType.Warning:
                    warnings++;
                    break;
                case LogEntryType.Error:
                    errors++;
                    break;
            }
        }

        // Calculate ratio (avoid division by zero)
        var ratio = stacks == 0 ? 0.0 : (double)dumps / stacks;

        return new PapyrusStats(DateTime.Now, dumps, stacks, warnings, errors, ratio);
    }

    /// <summary>
    /// Detects the text encoding of a log file using simple heuristics.
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The detected encoding name, or null if detection fails</returns>
    public async Task<string?> DetectEncodingAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read first few bytes to detect encoding
            var buffer = new byte[4096];
            using var stream = _fileSystem.File.OpenRead(filePath);
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            if (bytesRead == 0)
                return null;

            // Simple BOM detection
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                return "UTF-8";

            if (bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
                return "UTF-16LE";

            if (bytesRead >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
                return "UTF-16BE";

            // Default to UTF-8 if no BOM detected
            return "UTF-8";
        }
        catch
        {
            // If encoding detection fails, return null to use fallback
            return null;
        }
    }

    /// <summary>
    /// Determines the type of a log entry based on its content.
    /// </summary>
    /// <param name="line">The log line to analyze</param>
    /// <returns>The determined LogEntryType</returns>
    private static LogEntryType DetermineLogEntryType(string line)
    {
        // Check for dumps (order matters - "Dumping Stacks" contains "Dumping Stack")
        if (DumpingStacksRegex().IsMatch(line))
            return LogEntryType.DumpingStacks;

        if (DumpingStackRegex().IsMatch(line))
            return LogEntryType.DumpingStack;

        // Check for warnings and errors
        if (WarningRegex().IsMatch(line))
            return LogEntryType.Warning;

        if (ErrorRegex().IsMatch(line))
            return LogEntryType.Error;

        // Default to info
        return LogEntryType.Info;
    }
}