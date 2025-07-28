namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
/// Interface for file system watching functionality.
/// 
/// This interface provides an abstraction over file system watchers to enable
/// testability and provide consistent error handling for file monitoring scenarios.
/// </summary>
public interface IFileWatcher : IDisposable
{
    /// <summary>
    /// Gets an observable stream of file change events.
    /// Emitted when the watched file is modified, created, or deleted.
    /// </summary>
    IObservable<FileChangeEvent> FileChanged { get; }

    /// <summary>
    /// Gets an observable stream of file watcher errors.
    /// </summary>
    IObservable<string> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the file watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the path being watched.
    /// </summary>
    string? WatchPath { get; }

    /// <summary>
    /// Starts watching the specified file for changes.
    /// </summary>
    /// <param name="filePath">The path to the file to watch</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when watching starts</returns>
    Task StartWatchingAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops watching the file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when watching stops</returns>
    Task StopWatchingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a file change event from the file watcher.
/// </summary>
/// <param name="FilePath">The path of the file that changed</param>
/// <param name="ChangeType">The type of change that occurred</param>
/// <param name="Timestamp">When the change was detected</param>
public record FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    DateTime Timestamp);

/// <summary>
/// Types of file changes that can be detected.
/// </summary>
public enum FileChangeType
{
    /// <summary>Unknown change type</summary>
    Unknown,
    
    /// <summary>File was created</summary>
    Created,
    
    /// <summary>File was modified</summary>
    Modified,
    
    /// <summary>File was deleted</summary>
    Deleted,
    
    /// <summary>File was renamed</summary>
    Renamed
}