using System.IO.Abstractions.TestingHelpers;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Services;

public class PapyrusLogParserTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly PapyrusLogParser _parser;

    public PapyrusLogParserTests()
    {
        _fileSystem = new MockFileSystem();
        _parser = new PapyrusLogParser(_fileSystem);
    }

    [Fact]
    public void ParseLine_DumpingStacks_ReturnsCorrectType()
    {
        // Arrange
        const string logLine = "[12:34:56] Dumping Stacks to file";

        // Act
        var result = _parser.ParseLine(logLine, 1);

        // Assert
        Assert.Equal(LogEntryType.DumpingStacks, result.Type);
        Assert.Equal(logLine, result.Content);
        Assert.Equal(1, result.LineNumber);
    }

    [Fact]
    public void ParseLine_DumpingStack_ReturnsCorrectType()
    {
        // Arrange
        const string logLine = "[12:34:56] Dumping Stack frame info";

        // Act
        var result = _parser.ParseLine(logLine, 2);

        // Assert
        Assert.Equal(LogEntryType.DumpingStack, result.Type);
        Assert.Equal(logLine, result.Content);
        Assert.Equal(2, result.LineNumber);
    }

    [Fact]
    public void ParseLine_Warning_ReturnsCorrectType()
    {
        // Arrange
        const string logLine = "[12:34:56] Script warning: Something went wrong";

        // Act
        var result = _parser.ParseLine(logLine);

        // Assert
        Assert.Equal(LogEntryType.Warning, result.Type);
        Assert.Equal(logLine, result.Content);
    }

    [Fact]
    public void ParseLine_Error_ReturnsCorrectType()
    {
        // Arrange
        const string logLine = "[12:34:56] Script error: Critical failure";

        // Act
        var result = _parser.ParseLine(logLine);

        // Assert
        Assert.Equal(LogEntryType.Error, result.Type);
        Assert.Equal(logLine, result.Content);
    }

    [Fact]
    public void ParseLine_RegularLine_ReturnsInfoType()
    {
        // Arrange
        const string logLine = "[12:34:56] Regular log message";

        // Act
        var result = _parser.ParseLine(logLine);

        // Assert
        Assert.Equal(LogEntryType.Info, result.Type);
        Assert.Equal(logLine, result.Content);
    }

    [Fact]
    public void ParseLines_MultipleLines_ReturnsCorrectEntries()
    {
        // Arrange
        var lines = new[]
        {
            "Dumping Stacks to file",
            "Dumping Stack frame",
            "Script warning: test",
            "Script error: test",
            "Regular message"
        };

        // Act
        var results = _parser.ParseLines(lines).ToList();

        // Assert
        Assert.Equal(5, results.Count);
        Assert.Equal(LogEntryType.DumpingStacks, results[0].Type);
        Assert.Equal(LogEntryType.DumpingStack, results[1].Type);
        Assert.Equal(LogEntryType.Warning, results[2].Type);
        Assert.Equal(LogEntryType.Error, results[3].Type);
        Assert.Equal(LogEntryType.Info, results[4].Type);
    }

    [Fact]
    public void AggregateStats_CalculatesCorrectCounts()
    {
        // Arrange
        var entries = new[]
        {
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStacks),
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStacks),
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStack),
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStack),
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStack),
            new LogEntry(DateTime.Now, "test", LogEntryType.Warning),
            new LogEntry(DateTime.Now, "test", LogEntryType.Error),
            new LogEntry(DateTime.Now, "test", LogEntryType.Info)
        };

        // Act
        var stats = _parser.AggregateStats(entries);

        // Assert
        Assert.Equal(2, stats.Dumps);
        Assert.Equal(3, stats.Stacks);
        Assert.Equal(1, stats.Warnings);
        Assert.Equal(1, stats.Errors);
        Assert.Equal(2.0 / 3.0, stats.Ratio, 3); // 2 dumps / 3 stacks
    }

    [Fact]
    public void AggregateStats_NoStacks_RatioIsZero()
    {
        // Arrange
        var entries = new[]
        {
            new LogEntry(DateTime.Now, "test", LogEntryType.DumpingStacks),
            new LogEntry(DateTime.Now, "test", LogEntryType.Warning),
            new LogEntry(DateTime.Now, "test", LogEntryType.Error)
        };

        // Act
        var stats = _parser.AggregateStats(entries);

        // Assert
        Assert.Equal(1, stats.Dumps);
        Assert.Equal(0, stats.Stacks);
        Assert.Equal(1, stats.Warnings);
        Assert.Equal(1, stats.Errors);
        Assert.Equal(0.0, stats.Ratio);
    }

    [Fact]
    public async Task ParseFileAsync_NonExistentFile_ReturnsEmptyStats()
    {
        // Arrange
        const string filePath = @"C:\nonexistent\file.log";

        // Act
        var stats = await _parser.ParseFileAsync(filePath);

        // Assert
        Assert.Equal(0, stats.Dumps);
        Assert.Equal(0, stats.Stacks);
        Assert.Equal(0, stats.Warnings);
        Assert.Equal(0, stats.Errors);
        Assert.Equal(0.0, stats.Ratio);
    }

    [Fact]
    public async Task ParseFileAsync_ValidFile_ReturnsCorrectStats()
    {
        // Arrange
        const string filePath = @"C:\test\papyrus.log";
        const string content = @"[12:34:56] Starting game
Dumping Stacks to crash log
Dumping Stack frame 1
Dumping Stack frame 2
Script warning: Test warning
Script error: Test error
[13:00:00] Game ended";

        _fileSystem.AddFile(filePath, new MockFileData(content));

        // Act
        var stats = await _parser.ParseFileAsync(filePath);

        // Assert
        Assert.Equal(1, stats.Dumps);
        Assert.Equal(2, stats.Stacks);
        Assert.Equal(1, stats.Warnings);
        Assert.Equal(1, stats.Errors);
        Assert.Equal(0.5, stats.Ratio); // 1 dump / 2 stacks
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ParseFileAsync_InvalidPath_ThrowsArgumentException(string filePath)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _parser.ParseFileAsync(filePath));
    }
}