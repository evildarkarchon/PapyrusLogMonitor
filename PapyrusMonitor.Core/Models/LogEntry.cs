namespace PapyrusMonitor.Core.Models;

/// <summary>
/// Represents an individual log entry from a Papyrus log file.
/// 
/// This record encapsulates a single line or entry from the log file, along with
/// metadata about what type of entry it represents and when it was processed.
/// </summary>
/// <param name="Timestamp">When this log entry was processed</param>
/// <param name="Content">The raw content of the log line</param>
/// <param name="Type">The type of log entry (Dump, Stack, Warning, Error, etc.)</param>
/// <param name="LineNumber">The line number in the original log file (optional)</param>
public record LogEntry(
    DateTime Timestamp,
    string Content,
    LogEntryType Type,
    int? LineNumber = null);

/// <summary>
/// Enumeration of different types of log entries that can be found in Papyrus logs.
/// </summary>
public enum LogEntryType
{
    /// <summary>Unknown or unclassified log entry</summary>
    Unknown,
    
    /// <summary>Log entry containing "Dumping Stacks" - indicates stack dump</summary>
    DumpingStacks,
    
    /// <summary>Log entry containing "Dumping Stack" - indicates single stack dump</summary>
    DumpingStack,
    
    /// <summary>Log entry containing " warning: " - indicates a warning message</summary>
    Warning,
    
    /// <summary>Log entry containing " error: " - indicates an error message</summary>
    Error,
    
    /// <summary>Regular informational log entry</summary>
    Info
}