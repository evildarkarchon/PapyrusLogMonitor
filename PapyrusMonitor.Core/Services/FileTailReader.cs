using System.IO.Abstractions;
using System.Text;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Core.Services;

/// <summary>
/// Implementation of IFileTailReader that provides efficient tail-like reading of files.
/// 
/// This class tracks the current position in a file and only reads new content that has
/// been appended since the last read, making it efficient for monitoring large log files.
/// </summary>
public class FileTailReader : IFileTailReader
{
    private readonly IFileSystem _fileSystem;
    private long _currentPosition;
    private string? _filePath;
    private DateTime _lastModifiedTime;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileTailReader class.
    /// </summary>
    /// <param name="fileSystem">File system abstraction for testability</param>
    public FileTailReader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>
    /// Gets the current position in the file (in bytes).
    /// </summary>
    public long CurrentPosition => _currentPosition;

    /// <summary>
    /// Gets the path of the file being tailed.
    /// </summary>
    public string? FilePath => _filePath;

    /// <summary>
    /// Initializes the tail reader for the specified file.
    /// </summary>
    /// <param name="filePath">Path to the file to tail</param>
    /// <param name="startFromEnd">If true, starts reading from the end of the file; if false, starts from the beginning</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when initialization is finished</returns>
    public async Task InitializeAsync(string filePath, bool startFromEnd = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (_disposed)
            throw new ObjectDisposedException(nameof(FileTailReader));

        _filePath = filePath;

        if (_fileSystem.File.Exists(filePath))
        {
            var fileInfo = _fileSystem.FileInfo.New(filePath);
            _lastModifiedTime = fileInfo.LastWriteTime;

            if (startFromEnd)
            {
                _currentPosition = fileInfo.Length;
            }
            else
            {
                _currentPosition = 0;
            }
        }
        else
        {
            _currentPosition = 0;
            _lastModifiedTime = DateTime.MinValue;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Reads any new lines that have been added to the file since the last read.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A collection of new lines, or empty if no new content</returns>
    public async Task<IEnumerable<string>> ReadNewLinesAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || string.IsNullOrEmpty(_filePath))
            return Enumerable.Empty<string>();

        try
        {
            if (!_fileSystem.File.Exists(_filePath))
                return Enumerable.Empty<string>();

            var fileInfo = _fileSystem.FileInfo.New(_filePath);
            var currentLength = fileInfo.Length;

            // Check if file was truncated or recreated
            if (currentLength < _currentPosition)
            {
                await HandleFileRecreationAsync(cancellationToken);
                return await ReadNewLinesAsync(cancellationToken);
            }

            // No new content
            if (currentLength == _currentPosition)
                return Enumerable.Empty<string>();

            var newLines = new List<string>();

            // Read the new content
            using var stream = _fileSystem.File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(_currentPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                newLines.Add(line);
            }

            // Update position and timestamp
            _currentPosition = stream.Position;
            _lastModifiedTime = fileInfo.LastWriteTime;

            return newLines;
        }
        catch (IOException)
        {
            // File might be locked, return empty result
            return Enumerable.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied, return empty result
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Checks if the file has been modified since the last read.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>True if the file has new content to read</returns>
    public async Task<bool> HasNewContentAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || string.IsNullOrEmpty(_filePath))
            return false;

        try
        {
            if (!_fileSystem.File.Exists(_filePath))
                return false;

            var fileInfo = _fileSystem.FileInfo.New(_filePath);
            
            // Check if file size changed or was modified
            return fileInfo.Length != _currentPosition || fileInfo.LastWriteTime > _lastModifiedTime;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets the position to the beginning of the file.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the reset is finished</returns>
    public Task ResetPositionAsync(CancellationToken cancellationToken = default)
    {
        _currentPosition = 0;
        
        if (!string.IsNullOrEmpty(_filePath) && _fileSystem.File.Exists(_filePath))
        {
            var fileInfo = _fileSystem.FileInfo.New(_filePath);
            _lastModifiedTime = fileInfo.LastWriteTime;
        }
        else
        {
            _lastModifiedTime = DateTime.MinValue;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles file recreation scenarios (when the log file is deleted and recreated).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A task that completes when the file recreation is handled</returns>
    public async Task HandleFileRecreationAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_filePath))
            return;

        // Reset position to beginning since it's a new file
        await ResetPositionAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes the file tail reader and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}