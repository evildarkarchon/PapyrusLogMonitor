using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Core.Services;

/// <summary>
/// Implementation of IFileWatcher that provides file system monitoring with error handling.
/// 
/// This class wraps FileSystemWatcher functionality and provides observable streams
/// for file changes and errors, with proper cleanup and error recovery.
/// </summary>
public class FileWatcher : IFileWatcher
{
    private readonly IFileSystem _fileSystem;
    private readonly Subject<FileChangeEvent> _fileChangeSubject;
    private readonly Subject<string> _errorSubject;
    private IFileSystemWatcher? _watcher;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileWatcher class.
    /// </summary>
    /// <param name="fileSystem">File system abstraction for testability</param>
    public FileWatcher(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _fileChangeSubject = new Subject<FileChangeEvent>();
        _errorSubject = new Subject<string>();
    }

    /// <summary>
    /// Gets an observable stream of file change events.
    /// </summary>
    public IObservable<FileChangeEvent> FileChanged => _fileChangeSubject.AsObservable();

    /// <summary>
    /// Gets an observable stream of file watcher errors.
    /// </summary>
    public IObservable<string> Errors => _errorSubject.AsObservable();

    /// <summary>
    /// Gets a value indicating whether the file watcher is currently active.
    /// </summary>
    public bool IsWatching => _watcher?.EnableRaisingEvents == true;

    /// <summary>
    /// Gets the path being watched.
    /// </summary>
    public string? WatchPath { get; private set; }

    /// <summary>
    /// Starts watching the specified file for changes.
    /// </summary>
    /// <param name="filePath">The path to the file to watch</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when watching starts</returns>
    public Task StartWatchingAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (_disposed)
            throw new ObjectDisposedException(nameof(FileWatcher));

        try
        {
            // Stop any existing watcher
            StopWatching();

            var directory = _fileSystem.Path.GetDirectoryName(filePath);
            var fileName = _fileSystem.Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                _errorSubject.OnNext($"Invalid file path: {filePath}");
                return Task.CompletedTask;
            }

            // Create and configure the file system watcher
            _watcher = _fileSystem.FileSystemWatcher.New(directory, fileName);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;
            _watcher.IncludeSubdirectories = false;

            // Subscribe to events
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            // Start watching
            _watcher.EnableRaisingEvents = true;
            WatchPath = filePath;

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _errorSubject.OnNext($"Failed to start watching file {filePath}: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stops watching the file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when watching stops</returns>
    public Task StopWatchingAsync(CancellationToken cancellationToken = default)
    {
        StopWatching();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the file watcher and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopWatching();
        
        _fileChangeSubject?.Dispose();
        _errorSubject?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void StopWatching()
    {
        if (_watcher != null)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileCreated;
                _watcher.Deleted -= OnFileDeleted;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
            }
            catch (Exception ex)
            {
                _errorSubject.OnNext($"Error stopping file watcher: {ex.Message}");
            }
            finally
            {
                _watcher = null;
                WatchPath = null;
            }
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        EmitFileChangeEvent(e.FullPath, FileChangeType.Modified);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        EmitFileChangeEvent(e.FullPath, FileChangeType.Created);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        EmitFileChangeEvent(e.FullPath, FileChangeType.Deleted);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        EmitFileChangeEvent(e.FullPath, FileChangeType.Renamed);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var message = e.GetException()?.Message ?? "Unknown file watcher error";
        _errorSubject.OnNext($"File watcher error: {message}");
        
        // Try to restart the watcher after an error
        if (WatchPath != null)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000); // Wait a second before retrying
                try
                {
                    await StartWatchingAsync(WatchPath);
                }
                catch (Exception ex)
                {
                    _errorSubject.OnNext($"Failed to restart file watcher: {ex.Message}");
                }
            });
        }
    }

    private void EmitFileChangeEvent(string filePath, FileChangeType changeType)
    {
        if (!_disposed)
        {
            var changeEvent = new FileChangeEvent(filePath, changeType, DateTime.Now);
            _fileChangeSubject.OnNext(changeEvent);
        }
    }
}