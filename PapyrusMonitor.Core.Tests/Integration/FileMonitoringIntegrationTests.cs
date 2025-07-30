using System.IO.Abstractions.TestingHelpers;
using System.Text;
using FluentAssertions;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Integration;

/// <summary>
///     Integration tests for file monitoring scenarios
/// </summary>
public class FileMonitoringIntegrationTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly string _logFilePath = @"C:\Games\Fallout4\Logs\Script\Papyrus.0.log";

    public FileMonitoringIntegrationTests()
    {
        _fileSystem = new MockFileSystem();
    }

    [Fact]
    public async Task Parser_WithCompleteLogFile_ShouldExtractAllStats()
    {
        // Arrange
        CreateCompleteLogFile();
        var parser = new PapyrusLogParser(_fileSystem);

        // Act
        var stats = await parser.ParseFileAsync(_logFilePath);

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(3);
        stats.Stacks.Should().Be(5);
        stats.Warnings.Should().Be(2);
        stats.Errors.Should().Be(4);
        stats.Ratio.Should().BeApproximately(0.6, 0.01); // 3/5
    }

    [Fact]
    public async Task Parser_WithIncrementalUpdates_ShouldTrackChanges()
    {
        // Arrange
        CreateInitialLogFile();
        var parser = new PapyrusLogParser(_fileSystem);

        // Act - Parse initial content
        var initialStats = await parser.ParseFileAsync(_logFilePath);

        // Add more content
        AppendToLogFile("\n[07/29/2025 - 02:00:00PM] Dumping Stacks\n");
        AppendToLogFile("[07/29/2025 - 02:00:01PM] error: New error\n");
        AppendToLogFile("[07/29/2025 - 02:00:02PM] warning: New warning\n");

        var updatedStats = await parser.ParseFileAsync(_logFilePath);

        // Assert
        initialStats.Should().NotBeNull();
        updatedStats.Should().NotBeNull();

        updatedStats.Dumps.Should().BeGreaterThan(initialStats.Dumps);
        updatedStats.Errors.Should().BeGreaterThan(initialStats.Errors);
        updatedStats.Warnings.Should().BeGreaterThan(initialStats.Warnings);
    }

    [Fact]
    public async Task Parser_WithLargeLogFile_ShouldHandlePerformantly()
    {
        // Arrange
        CreateLargeLogFile(1000); // 1000 log entries
        var parser = new PapyrusLogParser(_fileSystem);

        // Act
        var startTime = DateTime.UtcNow;
        var stats = await parser.ParseFileAsync(_logFilePath);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().BeGreaterThan(0);
        stats.Errors.Should().BeGreaterThan(0);
        stats.Warnings.Should().BeGreaterThan(0);
        elapsed.TotalSeconds.Should().BeLessThan(1); // Should parse in under 1 second
    }

    [Fact]
    public async Task Parser_WithCorruptedEntries_ShouldSkipInvalidLines()
    {
        // Arrange
        CreateLogFileWithCorruptedEntries();
        var parser = new PapyrusLogParser(_fileSystem);

        // Act
        var stats = await parser.ParseFileAsync(_logFilePath);

        // Assert
        stats.Should().NotBeNull();
        // Should still parse valid entries
        stats.Dumps.Should().Be(1);
        stats.Errors.Should().Be(1);
        stats.Warnings.Should().Be(2); // Both warning lines are parsed
    }

    [Fact]
    public async Task Parser_WithMissingFile_ShouldReturnEmptyStats()
    {
        // Arrange
        var parser = new PapyrusLogParser(_fileSystem);
        var missingPath = @"C:\NonExistent\File.log";

        // Act
        var stats = await parser.ParseFileAsync(missingPath);

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(0);
        stats.Errors.Should().Be(0);
        stats.Warnings.Should().Be(0);
        stats.Stacks.Should().Be(0);
    }

    [Fact]
    public async Task Parser_WithEmptyFile_ShouldReturnEmptyStats()
    {
        // Arrange
        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        _fileSystem.File.WriteAllText(_logFilePath, string.Empty);
        var parser = new PapyrusLogParser(_fileSystem);

        // Act
        var stats = await parser.ParseFileAsync(_logFilePath);

        // Assert
        stats.Should().NotBeNull();
        stats.Dumps.Should().Be(0);
        stats.Errors.Should().Be(0);
        stats.Warnings.Should().Be(0);
        stats.Stacks.Should().Be(0);
    }

    [Fact]
    public void LogEntry_ParsedCorrectly_ShouldExtractAllFields()
    {
        // Arrange
        var parser = new PapyrusLogParser(_fileSystem);
        var logLines = new[]
        {
            "[07/29/2025 - 01:00:00PM] Dumping Stacks", "[07/29/2025 - 01:00:01PM] warning: Property not found",
            "[07/29/2025 - 01:00:02PM] error: Cannot call GetValue() on a None object", "[Invalid line format]",
            "Random text without timestamp"
        };

        // Act & Assert
        foreach (var line in logLines)
        {
            var entry = parser.ParseLine(line);

            if (line.StartsWith("[") && line.Contains("]"))
            {
                entry.Should().NotBeNull();
                entry!.Timestamp.Should().NotBe(default);
                entry.Content.Should().NotBeNullOrEmpty();

                if (line.Contains("Dumping stacks"))
                {
                    entry.Type.Should().Be(LogEntryType.DumpingStacks);
                }
                else if (line.Contains("warning:"))
                {
                    entry.Type.Should().Be(LogEntryType.Warning);
                }
                else if (line.Contains("error:"))
                {
                    entry.Type.Should().Be(LogEntryType.Error);
                }
            }
            else
            {
                // Parser returns non-null entries for all lines, just with different types
                entry.Should().NotBeNull();
                entry!.Type.Should().BeOneOf(LogEntryType.Info, LogEntryType.Unknown);
            }
        }
    }

    private void CreateInitialLogFile()
    {
        var initialContent = @"[07/29/2025 - 01:00:00PM] Papyrus log opened (PC-64)
[07/29/2025 - 01:00:01PM] Update budget: 1.200000ms (Extra tasklet budget: 1.200000ms, Load screen budget: 500.000000ms)
[07/29/2025 - 01:00:02PM] Dumping Stacks
[07/29/2025 - 01:00:03PM] warning: Property not found
[07/29/2025 - 01:00:04PM] error: Cannot call GetValue() on a None object
";
        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        _fileSystem.File.WriteAllText(_logFilePath, initialContent, Encoding.UTF8);
    }

    private void CreateCompleteLogFile()
    {
        var content = @"[07/29/2025 - 01:00:00PM] Papyrus log opened (PC-64)
[07/29/2025 - 01:00:01PM] VM is frozen
[07/29/2025 - 01:00:02PM] VM is thawed
[07/29/2025 - 01:00:03PM] Dumping Stacks
[07/29/2025 - 01:00:04PM] Dumping Stack 12345:
[07/29/2025 - 01:00:05PM] Frame count: 3
[07/29/2025 - 01:00:06PM] warning: Property not found
[07/29/2025 - 01:00:07PM] error: Cannot call GetValue() on a None object
[07/29/2025 - 01:00:08PM] Dumping Stacks
[07/29/2025 - 01:00:09PM] Dumping Stack 12346:
[07/29/2025 - 01:00:10PM] error: Array index out of bounds
[07/29/2025 - 01:00:11PM] warning: Deprecated function called
[07/29/2025 - 01:00:12PM] Dumping Stacks
[07/29/2025 - 01:00:13PM] Dumping Stack 12347:
[07/29/2025 - 01:00:14PM] error: Script instance has no parent
[07/29/2025 - 01:00:15PM] Dumping Stack 12348:
[07/29/2025 - 01:00:16PM] error: Failed to find function
[07/29/2025 - 01:00:17PM] Dumping Stack 12349:
";
        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        _fileSystem.File.WriteAllText(_logFilePath, content, Encoding.UTF8);
    }

    private void CreateLargeLogFile(int entryCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[07/29/2025 - 01:00:00PM] Papyrus log opened (PC-64)");

        var random = new Random(42);
        for (var i = 0; i < entryCount; i++)
        {
            var second = (i % 60).ToString("00");
            var minute = (i / 60 % 60).ToString("00");
            var hour = (1 + i / 3600).ToString("00");

            var entryType = random.Next(4);
            switch (entryType)
            {
                case 0:
                    sb.AppendLine($"[07/29/2025 - {hour}:{minute}:{second}PM] Dumping Stacks");
                    break;
                case 1:
                    sb.AppendLine($"[07/29/2025 - {hour}:{minute}:{second}PM] warning: Test warning {i}");
                    break;
                case 2:
                    sb.AppendLine($"[07/29/2025 - {hour}:{minute}:{second}PM] error: Test error {i}");
                    break;
                default:
                    sb.AppendLine($"[07/29/2025 - {hour}:{minute}:{second}PM] Normal log entry {i}");
                    break;
            }
        }

        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        _fileSystem.File.WriteAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
    }

    private void CreateLogFileWithCorruptedEntries()
    {
        var content = @"[07/29/2025 - 01:00:00PM] Papyrus log opened (PC-64)
[CORRUPTED DATA HERE]
[07/29/2025 - 01:00:02PM] Dumping Stacks
Some random text without timestamp
[07/29/2025 - 01:00:03PM] warning: Missing bracket
[07/29/2025 - 01:00:04PM] error: Normal error
��������� Binary data ���������
[07/29/2025 - 01:00:05PM] warning: Normal warning
[Malformed timestamp - XX:YY:ZZPM] Should not parse
";
        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        _fileSystem.File.WriteAllText(_logFilePath, content, Encoding.UTF8);
    }

    private void AppendToLogFile(string content)
    {
        var existingContent = _fileSystem.File.ReadAllText(_logFilePath);
        _fileSystem.File.WriteAllText(_logFilePath, existingContent + content, Encoding.UTF8);
    }
}
