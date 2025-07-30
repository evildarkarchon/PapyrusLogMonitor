using System.Reactive.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PapyrusMonitor.Core.Configuration;

namespace PapyrusMonitor.Core.Tests.Configuration;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly Mock<ILogger<JsonSettingsService>> _mockLogger;
    private readonly string _originalAppData;
    private readonly string _testDirectory;

    public JsonSettingsServiceTests()
    {
        _mockLogger = new Mock<ILogger<JsonSettingsService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PapyrusMonitorTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        // Override AppData location for tests
        _originalAppData = Environment.GetEnvironmentVariable("APPDATA") ?? string.Empty;
        Environment.SetEnvironmentVariable("APPDATA", _testDirectory);
    }

    public void Dispose()
    {
        // Restore original AppData
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);

        // Clean up test directory
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
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        using var service = new JsonSettingsService(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Settings.Should().NotBeNull();
    }

    [Fact]
    public void SettingsFilePath_ShouldReturnCorrectPath()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);

        // Act
        var path = service.SettingsFilePath;

        // Assert
        path.Should().Contain("PapyrusMonitor");
        path.Should().EndWith("settings.json");
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileDoesNotExist_ShouldReturnDefaultSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);

        // Act
        var settings = await service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.UpdateInterval.Should().Be(1000); // Default value
        settings.MaxLogEntries.Should().Be(10000); // Default value

        // Verify default settings were saved
        File.Exists(service.SettingsFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldSaveSettingsToFile()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var testSettings = new AppSettings
        {
            LogFilePath = @"C:\test\papyrus.log",
            UpdateInterval = 2000,
            AutoStartMonitoring = true,
            ShowErrorNotifications = true
        };

        // Act
        await service.SaveSettingsAsync(testSettings);

        // Assert
        File.Exists(service.SettingsFilePath).Should().BeTrue();
        var fileContent = await File.ReadAllTextAsync(service.SettingsFilePath);
        fileContent.Should().Contain("papyrus.log");
        fileContent.Should().Contain("2000");
        fileContent.Should().Contain("true");
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldUpdateCurrentSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var testSettings = new AppSettings { LogFilePath = @"C:\test\updated.log", UpdateInterval = 3000 };

        // Act
        await service.SaveSettingsAsync(testSettings);

        // Assert
        service.Settings.LogFilePath.Should().Be(@"C:\test\updated.log");
        service.Settings.UpdateInterval.Should().Be(3000);
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldEmitSettingsChangedEvent()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var testSettings = new AppSettings { UpdateInterval = 5000 };
        AppSettings? receivedSettings = null;

        service.SettingsChanged.Subscribe(s => receivedSettings = s);

        // Act
        await service.SaveSettingsAsync(testSettings);
        await Task.Delay(100); // Allow time for event to propagate

        // Assert
        receivedSettings.Should().NotBeNull();
        receivedSettings!.UpdateInterval.Should().Be(5000);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileExists_ShouldLoadSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var testSettings = new AppSettings
        {
            LogFilePath = @"C:\test\existing.log", UpdateInterval = 4000, MaxLogEntries = 5000
        };

        // First save settings
        await service.SaveSettingsAsync(testSettings);

        // Create new service instance to test loading
        using var newService = new JsonSettingsService(_mockLogger.Object);

        // Act
        var loadedSettings = await newService.LoadSettingsAsync();

        // Assert
        loadedSettings.Should().NotBeNull();
        loadedSettings.LogFilePath.Should().Be(@"C:\test\existing.log");
        loadedSettings.UpdateInterval.Should().Be(4000);
        loadedSettings.MaxLogEntries.Should().Be(5000);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileIsCorrupted_ShouldReturnDefaultSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var settingsPath = service.SettingsFilePath;

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write invalid JSON
        await File.WriteAllTextAsync(settingsPath, "{ invalid json }");

        // Act
        var settings = await service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.UpdateInterval.Should().Be(1000); // Default value

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to load settings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldResetAllSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);

        // First save custom settings
        var customSettings = new AppSettings
        {
            LogFilePath = @"C:\custom\path.log", UpdateInterval = 5000, ShowWarningNotifications = true
        };
        await service.SaveSettingsAsync(customSettings);

        // Act
        await service.ResetToDefaultsAsync();

        // Assert
        service.Settings.LogFilePath.Should().BeEmpty();
        service.Settings.UpdateInterval.Should().Be(1000);
        service.Settings.ShowWarningNotifications.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSettingsAsync_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveSettingsAsync(null!));
    }

    [Fact]
    public void Dispose_ShouldCompleteSettingsChangedObservable()
    {
        // Arrange
        var service = new JsonSettingsService(_mockLogger.Object);
        var completed = false;
        service.SettingsChanged.Subscribe(
            _ => { },
            () => completed = true);

        // Act
        service.Dispose();

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new JsonSettingsService(_mockLogger.Object);

        // Act & Assert
        service.Invoking(s =>
        {
            s.Dispose();
            s.Dispose();
            s.Dispose();
        }).Should().NotThrow();
    }

    [Fact]
    public async Task FileWatcher_WhenSettingsFileChanges_ShouldReloadSettings()
    {
        // Arrange
        using var service = new JsonSettingsService(_mockLogger.Object);
        var initialSettings = new AppSettings { UpdateInterval = 1000 };
        await service.SaveSettingsAsync(initialSettings);

        AppSettings? reloadedSettings = null;
        service.SettingsChanged.Skip(1).Subscribe(s => reloadedSettings = s); // Skip initial load

        // Act - Simulate external file change
        var updatedSettings = new AppSettings { UpdateInterval = 2000 };
        var json = JsonSerializer.Serialize(updatedSettings,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Wait a bit to ensure file watcher is ready
        await Task.Delay(200);
        await File.WriteAllTextAsync(service.SettingsFilePath, json);
        await Task.Delay(500); // Wait for file watcher to trigger and reload

        // Assert
        reloadedSettings?.UpdateInterval.Should().Be(2000);
    }
}
