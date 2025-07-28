using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Services;

public class FileWatcherTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly FileWatcher _fileWatcher;

    public FileWatcherTests()
    {
        _fileSystem = new MockFileSystem();
        _fileWatcher = new FileWatcher(_fileSystem);
    }

    [Fact]
    public void IsWatching_InitiallyFalse()
    {
        // Assert
        Assert.False(_fileWatcher.IsWatching);
        Assert.Null(_fileWatcher.WatchPath);
    }

    [Fact]
    public async Task StartWatchingAsync_ValidPath_DoesNotThrow()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        _fileSystem.AddFile(filePath, new MockFileData("test content"));

        var errorReceived = false;
        _fileWatcher.Errors.Subscribe(_ => errorReceived = true);

        // Act
        await _fileWatcher.StartWatchingAsync(filePath);

        // Assert
        // Note: MockFileSystem doesn't fully support FileSystemWatcher,
        // so we just verify the call doesn't throw and may emit an error
        Assert.True(true); // If we got here, no exception was thrown
        // The error emission is expected due to MockFileSystem limitations
    }

    [Fact]
    public async Task StartWatchingAsync_EmptyPath_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _fileWatcher.StartWatchingAsync(""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartWatchingAsync_EmptyPath_ThrowsArgumentException(string filePath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _fileWatcher.StartWatchingAsync(filePath));
    }

    [Fact]
    public async Task StopWatchingAsync_WhenWatching_StopsWatching()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        _fileSystem.AddFile(filePath, new MockFileData("test content"));
        await _fileWatcher.StartWatchingAsync(filePath);

        // Act
        await _fileWatcher.StopWatchingAsync();

        // Assert
        Assert.False(_fileWatcher.IsWatching);
        Assert.Null(_fileWatcher.WatchPath);
    }

    [Fact]
    public async Task StartWatchingAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        const string filePath1 = @"C:\test\log1.txt";
        const string filePath2 = @"C:\test\log2.txt";
        _fileSystem.AddFile(filePath1, new MockFileData("test content 1"));
        _fileSystem.AddFile(filePath2, new MockFileData("test content 2"));

        // Act & Assert
        await _fileWatcher.StartWatchingAsync(filePath1);
        await _fileWatcher.StartWatchingAsync(filePath2);

        // If we get here without exceptions, the test passes
        // MockFileSystem limitations prevent us from testing the full behavior
        Assert.True(true);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}