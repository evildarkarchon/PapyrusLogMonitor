using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Tests.Export;

public class ExportServiceTests : IDisposable
{
    private readonly AppSettings _defaultSettings;
    private readonly ExportService _exportService;
    private readonly Mock<ILogger<ExportService>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly string _testDirectory;

    public ExportServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExportService>>();
        _mockSettingsService = new Mock<ISettingsService>();

        _defaultSettings = new AppSettings
        {
            ExportSettings = new ExportSettings { IncludeTimestamps = true, DateFormat = "yyyy-MM-dd HH:mm:ss" }
        };

        _mockSettingsService.Setup(x => x.Settings).Returns(_defaultSettings);

        _exportService = new ExportService(_mockLogger.Object, _mockSettingsService.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"ExportServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task ExportAsync_ToFile_CSV_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "export.csv");
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, filePath, ExportFormat.Csv);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("Timestamp,Dumps,Stacks,Warnings,Errors,Ratio");
        content.Should().Contain("10,5,2,1,2.00");
    }

    [Fact]
    public async Task ExportAsync_ToFile_JSON_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "export.json");
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, filePath, ExportFormat.Json);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("\"Metadata\"");
        content.Should().Contain("\"Statistics\"");
        content.Should().Contain("\"Dumps\":10");
    }

    [Fact]
    public async Task ExportAsync_ToStream_CSV_ShouldWriteCorrectData()
    {
        // Arrange
        using var stream = new MemoryStream();
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, stream, ExportFormat.Csv);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("# Export Date:");
        content.Should().Contain("# Application Version: 1.0.0");
        content.Should().Contain("Timestamp,Dumps,Stacks,Warnings,Errors,Ratio");
        content.Should().Contain("10,5,2,1,2.00");
    }

    [Fact]
    public async Task ExportAsync_ToStream_JSON_ShouldWriteCorrectData()
    {
        // Arrange
        using var stream = new MemoryStream();
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, stream, ExportFormat.Json);

        // Assert
        stream.Position = 0;
        var jsonDocument = await JsonDocument.ParseAsync(stream);
        var root = jsonDocument.RootElement;

        root.TryGetProperty("Metadata", out _).Should().BeTrue();
        root.TryGetProperty("Statistics", out var stats).Should().BeTrue();
        stats.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task ExportAsync_CSV_WithoutTimestamps_ShouldExcludeTimestampColumn()
    {
        // Arrange
        var settings = new AppSettings { ExportSettings = new ExportSettings { IncludeTimestamps = false } };
        _mockSettingsService.Setup(x => x.Settings).Returns(settings);

        using var stream = new MemoryStream();
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, stream, ExportFormat.Csv);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("Dumps,Stacks,Warnings,Errors,Ratio");
        content.Should().NotContain("Timestamp,");
    }

    [Fact]
    public async Task ExportAsync_CSV_WithSummary_ShouldIncludeSummaryData()
    {
        // Arrange
        using var stream = new MemoryStream();
        var exportData = CreateTestExportDataWithSummary();

        // Act
        await _exportService.ExportAsync(exportData, stream, ExportFormat.Csv);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().Contain("# Summary Statistics");
        content.Should().Contain("# Total Dumps: 100");
        content.Should().Contain("# Average Ratio: 1.50");
        content.Should().Contain("# Duration: 01:30:00");
    }

    [Fact]
    public async Task ExportAsync_WithNullData_ShouldThrowArgumentNullException()
    {
        // Arrange
        ExportData? nullData = null;
        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exportService.ExportAsync(nullData!, stream, ExportFormat.Csv));
    }

    [Fact]
    public async Task ExportAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        var exportData = CreateTestExportData();
        Stream? nullStream = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _exportService.ExportAsync(exportData, nullStream!, ExportFormat.Csv));
    }

    [Fact]
    public async Task ExportAsync_WithEmptyFilePath_ShouldThrowArgumentException()
    {
        // Arrange
        var exportData = CreateTestExportData();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exportService.ExportAsync(exportData, string.Empty, ExportFormat.Csv));
    }

    [Fact]
    public async Task ExportAsync_WithInvalidFormat_ShouldThrowNotSupportedException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var exportData = CreateTestExportData();
        var invalidFormat = (ExportFormat)999;

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _exportService.ExportAsync(exportData, stream, invalidFormat));
    }

    [Fact]
    public void GetFileExtension_ShouldReturnCorrectExtensions()
    {
        // Act & Assert
        _exportService.GetFileExtension(ExportFormat.Csv).Should().Be(".csv");
        _exportService.GetFileExtension(ExportFormat.Json).Should().Be(".json");
    }

    [Fact]
    public void GetFileExtension_WithInvalidFormat_ShouldThrowNotSupportedException()
    {
        // Arrange
        var invalidFormat = (ExportFormat)999;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _exportService.GetFileExtension(invalidFormat));
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectory_WhenNotExists()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "subdir");
        var filePath = Path.Combine(subDir, "export.csv");
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, filePath, ExportFormat.Csv);

        // Assert
        Directory.Exists(subDir).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var exportData = CreateLargeExportData(); // Many records
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _exportService.ExportAsync(exportData, stream, ExportFormat.Csv, cts.Token));
    }

    [Fact]
    public async Task ExportAsync_JSON_WithoutTimestamps_ShouldExcludeTimestamps()
    {
        // Arrange
        var settings = new AppSettings { ExportSettings = new ExportSettings { IncludeTimestamps = false } };
        _mockSettingsService.Setup(x => x.Settings).Returns(settings);

        using var stream = new MemoryStream();
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, stream, ExportFormat.Json);

        // Assert
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        content.Should().NotContain("\"Timestamp\"");
        content.Should().Contain("\"Dumps\"");
        content.Should().Contain("\"Stacks\"");
    }

    [Fact]
    public async Task ExportAsync_LogsSuccessfulExport()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        var exportData = CreateTestExportData();

        // Act
        await _exportService.ExportAsync(exportData, filePath, ExportFormat.Csv);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Successfully exported")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private ExportData CreateTestExportData()
    {
        return new ExportData
        {
            Metadata = new ExportMetadata
            {
                ExportDate = new DateTime(2024, 1, 1, 12, 0, 0),
                ApplicationVersion = "1.0.0",
                LogFilePath = @"C:\test\papyrus.log"
            },
            Statistics = new List<PapyrusStats>
            {
                new(new DateTime(2024, 1, 1, 12, 0, 0), 10, 5, 2, 1, 2.0),
                new(new DateTime(2024, 1, 1, 12, 1, 0), 15, 8, 3, 2, 1.875)
            }
        };
    }

    private ExportData CreateTestExportDataWithSummary()
    {
        var data = CreateTestExportData();
        return data with
        {
            Summary = new SessionSummary
            {
                TotalDumps = 100,
                TotalStacks = 50,
                TotalWarnings = 10,
                TotalErrors = 5,
                AverageRatio = 1.5,
                PeakDumps = 20,
                PeakStacks = 15,
                Duration = TimeSpan.FromMinutes(90)
            }
        };
    }

    private ExportData CreateLargeExportData()
    {
        var stats = new List<PapyrusStats>();
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0);

        for (var i = 0; i < 10000; i++)
        {
            stats.Add(new PapyrusStats(
                baseTime.AddSeconds(i),
                10 + i % 10,
                5 + i % 5,
                i % 3,
                i % 2,
                2.0 + i % 10 * 0.1));
        }

        return new ExportData { Metadata = new ExportMetadata(), Statistics = stats };
    }
}
