namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
/// Interface for reading new content from files as they are appended to (tail-like functionality).
/// 
/// This interface provides efficient reading of file changes by only processing new content
/// that has been added since the last read, avoiding the need to re-read entire large files.
/// </summary>
public interface IFileTailReader : IDisposable
{
    /// <summary>
    /// Gets the current position in the file (in bytes).
    /// </summary>
    long CurrentPosition { get; }

    /// <summary>
    /// Gets the path of the file being tailed.
    /// </summary>
    string? FilePath { get; }

    /// <summary>
    /// Initializes the tail reader for the specified file.
    /// </summary>
    /// <param name="filePath">Path to the file to tail</param>
    /// <param name="startFromEnd">If true, starts reading from the end of the file; if false, starts from the beginning</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when initialization is finished</returns>
    Task InitializeAsync(string filePath, bool startFromEnd = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads any new lines that have been added to the file since the last read.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A collection of new lines, or empty if no new content</returns>
    Task<IEnumerable<string>> ReadNewLinesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the file has been modified since the last read.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>True if the file has new content to read</returns>
    Task<bool> HasNewContentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the position to the beginning of the file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the reset is finished</returns>
    Task ResetPositionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles file recreation scenarios (when the log file is deleted and recreated).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the file recreation is handled</returns>
    Task HandleFileRecreationAsync(CancellationToken cancellationToken = default);
}