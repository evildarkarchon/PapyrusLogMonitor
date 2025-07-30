using System.Text.Json;
using FluentAssertions;
using PapyrusMonitor.Core.Configuration;

namespace PapyrusMonitor.Core.Tests.Configuration;

public class AppSettingsTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public AppSettingsTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
        };
    }

    [Fact]
    public void AppSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new AppSettings();

        // Assert
        settings.LogFilePath.Should().BeEmpty();
        settings.UpdateInterval.Should().Be(1000);
        settings.AutoStartMonitoring.Should().BeFalse();
        settings.MaxLogEntries.Should().Be(10000);
        settings.ShowErrorNotifications.Should().BeTrue();
        settings.ShowWarningNotifications.Should().BeFalse();
        settings.ExportSettings.Should().NotBeNull();
        settings.WindowSettings.Should().NotBeNull();
    }

    [Fact]
    public void AppSettings_WithInitializer_ShouldSetPropertiesCorrectly()
    {
        // Act
        var settings = new AppSettings
        {
            LogFilePath = @"C:\test\log.txt",
            UpdateInterval = 2000,
            AutoStartMonitoring = true,
            MaxLogEntries = 5000,
            ShowErrorNotifications = false,
            ShowWarningNotifications = true
        };

        // Assert
        settings.LogFilePath.Should().Be(@"C:\test\log.txt");
        settings.UpdateInterval.Should().Be(2000);
        settings.AutoStartMonitoring.Should().BeTrue();
        settings.MaxLogEntries.Should().Be(5000);
        settings.ShowErrorNotifications.Should().BeFalse();
        settings.ShowWarningNotifications.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_JsonSerialization_ShouldWorkCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            LogFilePath = @"C:\game\logs\papyrus.0.log", UpdateInterval = 3000, AutoStartMonitoring = true
        };

        // Act
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.LogFilePath.Should().Be(settings.LogFilePath);
        deserialized.UpdateInterval.Should().Be(settings.UpdateInterval);
        deserialized.AutoStartMonitoring.Should().Be(settings.AutoStartMonitoring);
    }

    [Fact]
    public void AppSettings_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var settings1 = new AppSettings { LogFilePath = "test.log", UpdateInterval = 2000 };
        var settings2 = new AppSettings { LogFilePath = "test.log", UpdateInterval = 2000 };
        var settings3 = new AppSettings { LogFilePath = "different.log", UpdateInterval = 2000 };

        // Assert
        settings1.Should().Be(settings2);
        settings1.Should().NotBe(settings3);
    }

    [Fact]
    public void ExportSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var exportSettings = new ExportSettings();

        // Assert
        exportSettings.DefaultExportPath.Should().BeEmpty();
        exportSettings.IncludeTimestamps.Should().BeTrue();
        exportSettings.DateFormat.Should().Be("yyyy-MM-dd HH:mm:ss");
    }

    [Fact]
    public void ExportSettings_WithInitializer_ShouldSetPropertiesCorrectly()
    {
        // Act
        var exportSettings = new ExportSettings
        {
            DefaultExportPath = @"C:\exports", IncludeTimestamps = false, DateFormat = "MM/dd/yyyy"
        };

        // Assert
        exportSettings.DefaultExportPath.Should().Be(@"C:\exports");
        exportSettings.IncludeTimestamps.Should().BeFalse();
        exportSettings.DateFormat.Should().Be("MM/dd/yyyy");
    }

    [Fact]
    public void WindowSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var windowSettings = new WindowSettings();

        // Assert
        windowSettings.X.Should().Be(-1);
        windowSettings.Y.Should().Be(-1);
        windowSettings.Width.Should().Be(800);
        windowSettings.Height.Should().Be(600);
        windowSettings.IsMaximized.Should().BeFalse();
    }

    [Fact]
    public void WindowSettings_WithInitializer_ShouldSetPropertiesCorrectly()
    {
        // Act
        var windowSettings = new WindowSettings
        {
            X = 100,
            Y = 200,
            Width = 1024,
            Height = 768,
            IsMaximized = true
        };

        // Assert
        windowSettings.X.Should().Be(100);
        windowSettings.Y.Should().Be(200);
        windowSettings.Width.Should().Be(1024);
        windowSettings.Height.Should().Be(768);
        windowSettings.IsMaximized.Should().BeTrue();
    }

    [Fact]
    public void AppSettings_WithNestedSettings_ShouldSerializeCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            LogFilePath = @"C:\test.log",
            ExportSettings = new ExportSettings { DefaultExportPath = @"C:\exports", DateFormat = "ISO" },
            WindowSettings = new WindowSettings { X = 50, Y = 50, Width = 1200, Height = 800 }
        };

        // Act
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ExportSettings.DefaultExportPath.Should().Be(@"C:\exports");
        deserialized.ExportSettings.DateFormat.Should().Be("ISO");
        deserialized.WindowSettings.X.Should().Be(50);
        deserialized.WindowSettings.Y.Should().Be(50);
        deserialized.WindowSettings.Width.Should().Be(1200);
        deserialized.WindowSettings.Height.Should().Be(800);
    }

    [Fact]
    public void AppSettings_JsonPropertyNames_ShouldBeCamelCase()
    {
        // Arrange
        var settings = new AppSettings
        {
            LogFilePath = "test.log", AutoStartMonitoring = true, ShowErrorNotifications = false
        };

        // Act
        var json = JsonSerializer.Serialize(settings, _jsonOptions);

        // Assert
        json.Should().Contain("\"logFilePath\"");
        json.Should().Contain("\"autoStartMonitoring\"");
        json.Should().Contain("\"showErrorNotifications\"");
        json.Should().Contain("\"exportSettings\"");
        json.Should().Contain("\"windowSettings\"");
    }
}
