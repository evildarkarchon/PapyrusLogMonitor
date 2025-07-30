using System.IO.Abstractions.TestingHelpers;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Services;

public class PapyrusMonitorServiceTests : IDisposable
{
    private readonly MonitoringConfiguration _configuration;
    private readonly MockFileSystem _fileSystem;
    private readonly FileWatcher _fileWatcher;
    private readonly PapyrusLogParser _logParser;
    private readonly PapyrusMonitorService _monitorService;
    private readonly FileTailReader _tailReader;

    public PapyrusMonitorServiceTests()
    {
        _fileSystem = new MockFileSystem();
        _logParser = new PapyrusLogParser(_fileSystem);
        _fileWatcher = new FileWatcher(_fileSystem);
        _tailReader = new FileTailReader(_fileSystem);
        _configuration = new MonitoringConfiguration(@"C:\test\papyrus.log")
        {
            UseFileWatcher = false, // Use polling for predictable testing
            UpdateIntervalMs = 100
        };
        _monitorService = new PapyrusMonitorService(_logParser, _fileWatcher, _tailReader, _configuration);
    }

    public void Dispose()
    {
        try
        {
            _monitorService?.Dispose();
            _fileWatcher?.Dispose();
            _tailReader?.Dispose();

            // Give a small delay to ensure async operations complete
            Thread.Sleep(50);
        }
        catch
        {
            // Ignore disposal exceptions in tests
        }
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        // Assert
        Assert.False(_monitorService.IsMonitoring);
        Assert.Equal(_configuration, _monitorService.Configuration);
        Assert.Null(_monitorService.LastStats);
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_StartsMonitoring()
    {
        // Arrange
        const string logContent = @"[12:34:56] Starting game
Dumping Stacks to crash log
Script warning: Test warning";

        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData(logContent));

        // Act
        await _monitorService.StartAsync();

        // Assert
        Assert.True(_monitorService.IsMonitoring);
        Assert.NotNull(_monitorService.LastStats);
    }

    [Fact]
    public async Task StartAsync_WithInvalidConfiguration_EmitsError()
    {
        // Arrange
        var errorReceived = false;
        string? errorMessage = null;

        _monitorService.Errors.Subscribe(error =>
        {
            errorReceived = true;
            errorMessage = error;
        });

        var invalidConfig = new MonitoringConfiguration
        {
            LogFilePath = null // Invalid - no log file path
        };

        await _monitorService.UpdateConfigurationAsync(invalidConfig);

        // Act
        await _monitorService.StartAsync();

        // Assert
        Assert.True(errorReceived);
        Assert.Contains("Log file path is not configured", errorMessage);
    }

    [Fact]
    public async Task ForceUpdateAsync_WithLogFile_EmitsStats()
    {
        // Arrange
        PapyrusStats? receivedStats = null;
        var statsReceived = false;

        _monitorService.StatsUpdated.Subscribe(stats =>
        {
            receivedStats = stats;
            statsReceived = true;
        });

        const string logContent = @"Dumping Stacks to file
Dumping Stack frame 1
Dumping Stack frame 2
Script warning: Test warning
Script error: Test error";

        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData(logContent));

        // Act
        await _monitorService.ForceUpdateAsync();

        // Assert
        Assert.True(statsReceived);
        Assert.NotNull(receivedStats);
        Assert.Equal(1, receivedStats.Dumps);
        Assert.Equal(2, receivedStats.Stacks);
        Assert.Equal(1, receivedStats.Warnings);
        Assert.Equal(1, receivedStats.Errors);
        Assert.Equal(0.5, receivedStats.Ratio); // 1 dump / 2 stacks
    }

    [Fact]
    public async Task ForceUpdateAsync_SameStats_DoesNotEmitDuplicate()
    {
        // Arrange
        var statsCount = 0;
        _monitorService.StatsUpdated.Subscribe(_ => statsCount++);

        const string logContent = "Dumping Stacks to file";
        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData(logContent));

        // Act
        await _monitorService.ForceUpdateAsync();
        await _monitorService.ForceUpdateAsync(); // Second call with same content

        // Assert
        Assert.Equal(1, statsCount); // Should only emit once
    }

    [Fact]
    public async Task StopAsync_WhenMonitoring_StopsMonitoring()
    {
        // Arrange
        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData("test content"));
        await _monitorService.StartAsync();

        // Act
        await _monitorService.StopAsync();

        // Assert
        Assert.False(_monitorService.IsMonitoring);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WhileMonitoring_RestartsWithNewConfig()
    {
        // Arrange
        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData("test content"));
        await _monitorService.StartAsync();

        var newConfig = new MonitoringConfiguration(@"C:\test\new_papyrus.log")
        {
            UseFileWatcher = false, UpdateIntervalMs = 200
        };

        // Act
        await _monitorService.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.Equal(newConfig, _monitorService.Configuration);
        Assert.True(_monitorService.IsMonitoring); // Should still be monitoring
    }

    [Fact]
    public async Task Dispose_CleansUpResources()
    {
        // Arrange
        _fileSystem.AddFile(_configuration.LogFilePath!, new MockFileData("test content"));
        await _monitorService.StartAsync();

        // Act
        _monitorService.Dispose();

        // Assert
        Assert.False(_monitorService.IsMonitoring);
    }
}
