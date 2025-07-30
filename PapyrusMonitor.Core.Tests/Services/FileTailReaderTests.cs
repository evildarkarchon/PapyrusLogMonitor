using System.IO.Abstractions.TestingHelpers;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Services;

public class FileTailReaderTests : IDisposable
{
    private readonly MockFileSystem _fileSystem;
    private readonly FileTailReader _tailReader;

    public FileTailReaderTests()
    {
        _fileSystem = new MockFileSystem();
        _tailReader = new FileTailReader(_fileSystem);
    }

    public void Dispose()
    {
        _tailReader?.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_ValidFile_SetsProperties()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\nLine 2\nLine 3\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        // Act
        await _tailReader.InitializeAsync(filePath);

        // Assert
        Assert.Equal(filePath, _tailReader.FilePath);
        Assert.Equal(0, _tailReader.CurrentPosition);
    }

    [Fact]
    public async Task InitializeAsync_StartFromEnd_SetsPositionToEnd()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\nLine 2\nLine 3\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        // Act
        await _tailReader.InitializeAsync(filePath, true);

        // Assert
        Assert.Equal(filePath, _tailReader.FilePath);
        Assert.Equal(content.Length, _tailReader.CurrentPosition);
    }

    [Fact]
    public async Task InitializeAsync_NonExistentFile_SetsZeroPosition()
    {
        // Arrange
        const string filePath = @"C:\test\nonexistent.txt";

        // Act
        await _tailReader.InitializeAsync(filePath);

        // Assert
        Assert.Equal(filePath, _tailReader.FilePath);
        Assert.Equal(0, _tailReader.CurrentPosition);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InitializeAsync_InvalidPath_ThrowsArgumentException(string filePath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _tailReader.InitializeAsync(filePath));
    }

    [Fact]
    public async Task ReadNewLinesAsync_NewContent_ReturnsNewLines()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string initialContent = "Line 1\nLine 2\n";
        _fileSystem.AddFile(filePath, new MockFileData(initialContent));

        await _tailReader.InitializeAsync(filePath);
        var initialLines = await _tailReader.ReadNewLinesAsync();

        // Add more content
        const string additionalContent = "Line 3\nLine 4\n";
        _fileSystem.File.AppendAllText(filePath, additionalContent);

        // Act
        var newLines = await _tailReader.ReadNewLinesAsync();

        // Assert
        Assert.Equal(2, initialLines.Count());
        Assert.Equal(2, newLines.Count());
        Assert.Contains("Line 3", newLines);
        Assert.Contains("Line 4", newLines);
    }

    [Fact]
    public async Task ReadNewLinesAsync_NoNewContent_ReturnsEmpty()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\nLine 2\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        await _tailReader.InitializeAsync(filePath);
        await _tailReader.ReadNewLinesAsync(); // Read initial content

        // Act
        var newLines = await _tailReader.ReadNewLinesAsync();

        // Assert
        Assert.Empty(newLines);
    }

    [Fact]
    public async Task HasNewContentAsync_WithNewContent_ReturnsTrue()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string initialContent = "Line 1\n";
        _fileSystem.AddFile(filePath, new MockFileData(initialContent));

        await _tailReader.InitializeAsync(filePath, true);

        // Add more content
        _fileSystem.File.AppendAllText(filePath, "Line 2\n");

        // Act
        var hasNewContent = await _tailReader.HasNewContentAsync();

        // Assert
        Assert.True(hasNewContent);
    }

    [Fact]
    public async Task HasNewContentAsync_NoNewContent_ReturnsFalse()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        await _tailReader.InitializeAsync(filePath, true);

        // Act
        var hasNewContent = await _tailReader.HasNewContentAsync();

        // Assert
        Assert.False(hasNewContent);
    }

    [Fact]
    public async Task ResetPositionAsync_ResetsToBeginning()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\nLine 2\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        await _tailReader.InitializeAsync(filePath, true);
        var initialPosition = _tailReader.CurrentPosition;

        // Act
        await _tailReader.ResetPositionAsync();

        // Assert
        Assert.True(initialPosition > 0);
        Assert.Equal(0, _tailReader.CurrentPosition);
    }

    [Fact]
    public async Task HandleFileRecreationAsync_ResetsPosition()
    {
        // Arrange
        const string filePath = @"C:\test\log.txt";
        const string content = "Line 1\nLine 2\n";
        _fileSystem.AddFile(filePath, new MockFileData(content));

        await _tailReader.InitializeAsync(filePath);
        await _tailReader.ReadNewLinesAsync(); // Advance position

        var positionBeforeRecreation = _tailReader.CurrentPosition;

        // Act
        await _tailReader.HandleFileRecreationAsync();

        // Assert
        Assert.True(positionBeforeRecreation > 0);
        Assert.Equal(0, _tailReader.CurrentPosition);
    }
}
