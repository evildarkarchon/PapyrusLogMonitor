using PapyrusMonitor.Core.Configuration;

namespace PapyrusMonitor.Core.Tests.Configuration;

public class MonitoringConfigurationTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var config = new MonitoringConfiguration();

        // Assert
        Assert.Null(config.LogFilePath);
        Assert.Equal(1000, config.UpdateIntervalMs);
        Assert.Equal(10_000, config.MaxLogEntries);
        Assert.True(config.UseFileWatcher);
        Assert.Equal(0.5, config.WarningRatioThreshold);
        Assert.Equal(0.8, config.ErrorRatioThreshold);
        Assert.True(config.AutoDetectEncoding);
        Assert.Equal("UTF-8", config.FallbackEncoding);
    }

    [Fact]
    public void ParameterizedConstructor_SetsLogFilePath()
    {
        // Arrange
        const string testPath = @"C:\test\papyrus.log";

        // Act
        var config = new MonitoringConfiguration(testPath);

        // Assert
        Assert.Equal(testPath, config.LogFilePath);
        // Other properties should still have defaults
        Assert.Equal(1000, config.UpdateIntervalMs);
        Assert.True(config.UseFileWatcher);
    }

    [Fact]
    public void Validate_ValidConfiguration_ReturnsNoErrors()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            UpdateIntervalMs = 500,
            MaxLogEntries = 5000,
            WarningRatioThreshold = 0.3,
            ErrorRatioThreshold = 0.7,
            FallbackEncoding = "UTF-8"
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidUpdateInterval_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            UpdateIntervalMs = 0
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("UpdateIntervalMs must be greater than 0", errors);
    }

    [Fact]
    public void Validate_InvalidMaxLogEntries_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            MaxLogEntries = -1
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("MaxLogEntries must be greater than 0", errors);
    }

    [Fact]
    public void Validate_NegativeWarningThreshold_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            WarningRatioThreshold = -0.1
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("WarningRatioThreshold must be non-negative", errors);
    }

    [Fact]
    public void Validate_NegativeErrorThreshold_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            ErrorRatioThreshold = -0.1
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("ErrorRatioThreshold must be non-negative", errors);
    }

    [Fact]
    public void Validate_ErrorThresholdLessThanWarningThreshold_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            WarningRatioThreshold = 0.8,
            ErrorRatioThreshold = 0.5
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("ErrorRatioThreshold must be greater than WarningRatioThreshold", errors);
    }

    [Fact]
    public void Validate_EmptyFallbackEncoding_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            FallbackEncoding = ""
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("FallbackEncoding cannot be null or empty", errors);
    }

    [Fact]
    public void Validate_NullFallbackEncoding_ReturnsError()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            FallbackEncoding = null!
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("FallbackEncoding cannot be null or empty", errors);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = new MonitoringConfiguration
        {
            UpdateIntervalMs = 0,
            MaxLogEntries = -1,
            WarningRatioThreshold = 0.8,
            ErrorRatioThreshold = 0.5,
            FallbackEncoding = ""
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Equal(4, errors.Count);
        Assert.Contains("UpdateIntervalMs must be greater than 0", errors);
        Assert.Contains("MaxLogEntries must be greater than 0", errors);
        Assert.Contains("ErrorRatioThreshold must be greater than WarningRatioThreshold", errors);
        Assert.Contains("FallbackEncoding cannot be null or empty", errors);
    }
}