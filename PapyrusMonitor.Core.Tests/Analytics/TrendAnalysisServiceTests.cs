using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Tests.Analytics;

public class TrendAnalysisServiceTests
{
    private readonly Mock<ILogger<TrendAnalysisService>> _mockLogger;
    private readonly TrendAnalysisService _service;

    public TrendAnalysisServiceTests()
    {
        _mockLogger = new Mock<ILogger<TrendAnalysisService>>();
        _service = new TrendAnalysisService(_mockLogger.Object);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_WithNullStatistics_ReturnsEmptyResult()
    {
        // Act
        var result = await _service.AnalyzeTrendsAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.DumpsTrend.DataPoints.Should().BeEmpty();
        result.StacksTrend.DataPoints.Should().BeEmpty();
        result.WarningsTrend.DataPoints.Should().BeEmpty();
        result.ErrorsTrend.DataPoints.Should().BeEmpty();
        result.RatioTrend.DataPoints.Should().BeEmpty();

        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No statistics provided")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_WithEmptyStatistics_ReturnsEmptyResult()
    {
        // Arrange
        var emptyStats = new List<PapyrusStats>();

        // Act
        var result = await _service.AnalyzeTrendsAsync(emptyStats);

        // Assert
        result.Should().NotBeNull();
        result.DumpsTrend.DataPoints.Should().BeEmpty();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No statistics provided")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_WithSingleDataPoint_ReturnsValidResult()
    {
        // Arrange
        var stats = new List<PapyrusStats> { new(DateTime.Now, 10, 5, 2, 1, 0.1) };

        // Act
        var result = await _service.AnalyzeTrendsAsync(stats);

        // Assert
        result.Should().NotBeNull();
        result.DumpsTrend.DataPoints.Should().HaveCount(1);
        result.DumpsTrend.DataPoints[0].Value.Should().Be(10);
        result.DumpsTrend.MovingAverage.Should().HaveCount(1);
        result.DumpsTrend.MovingAverage[0].Value.Should().Be(10); // Single point average is itself
        result.DumpsTrend.TrendLine.Should().HaveCount(1);
        result.TimeRange.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_WithMultipleDataPoints_CalculatesCorrectTrends()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var stats = new List<PapyrusStats>
        {
            new(baseTime, 10, 5, 2, 1, 0.1),
            new(baseTime.AddMinutes(1), 20, 10, 4, 2, 0.2),
            new(baseTime.AddMinutes(2), 30, 15, 6, 3, 0.3),
            new(baseTime.AddMinutes(3), 40, 20, 8, 4, 0.4),
            new(baseTime.AddMinutes(4), 50, 25, 10, 5, 0.5)
        };

        // Act
        var result = await _service.AnalyzeTrendsAsync(stats, 3);

        // Assert
        result.Should().NotBeNull();

        // Verify dumps trend
        result.DumpsTrend.DataPoints.Should().HaveCount(5);
        result.DumpsTrend.DataPoints.Select(dp => dp.Value).Should()
            .BeEquivalentTo(new[] { 10.0, 20.0, 30.0, 40.0, 50.0 });

        // Verify moving average calculation
        result.DumpsTrend.MovingAverage.Should().HaveCount(5);
        result.DumpsTrend.MovingAverage[0].Value.Should().Be(10.0); // First point
        result.DumpsTrend.MovingAverage[1].Value.Should().Be(15.0); // (10+20)/2
        result.DumpsTrend.MovingAverage[2].Value.Should().Be(20.0); // (10+20+30)/3
        result.DumpsTrend.MovingAverage[3].Value.Should().Be(30.0); // (20+30+40)/3
        result.DumpsTrend.MovingAverage[4].Value.Should().Be(40.0); // (30+40+50)/3

        // Verify trend line exists
        result.DumpsTrend.TrendLine.Should().HaveCount(5);

        // Verify summary
        result.DumpsTrend.Summary.Should().NotBeNull();
        result.DumpsTrend.Summary.Min.Should().Be(10);
        result.DumpsTrend.Summary.Max.Should().Be(50);
        result.DumpsTrend.Summary.Average.Should().Be(30);
        result.DumpsTrend.Summary.TrendSlope.Should().BeGreaterThan(0); // Positive trend

        // Verify time range
        result.TimeRange.Start.Should().Be(baseTime);
        result.TimeRange.End.Should().Be(baseTime.AddMinutes(4));
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_AllMetricsAnalyzedInParallel()
    {
        // Arrange
        var stats = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1), new(DateTime.Now.AddMinutes(1), 15, 8, 3, 2, 0.13)
        };

        // Act
        var result = await _service.AnalyzeTrendsAsync(stats);

        // Assert
        result.DumpsTrend.Should().NotBeNull();
        result.StacksTrend.Should().NotBeNull();
        result.WarningsTrend.Should().NotBeNull();
        result.ErrorsTrend.Should().NotBeNull();
        result.RatioTrend.Should().NotBeNull();

        // Verify each metric has correct values
        result.DumpsTrend.DataPoints.Select(dp => dp.Value).Should().BeEquivalentTo(new[] { 10.0, 15.0 });
        result.StacksTrend.DataPoints.Select(dp => dp.Value).Should().BeEquivalentTo(new[] { 5.0, 8.0 });
        result.WarningsTrend.DataPoints.Select(dp => dp.Value).Should().BeEquivalentTo(new[] { 2.0, 3.0 });
        result.ErrorsTrend.DataPoints.Select(dp => dp.Value).Should().BeEquivalentTo(new[] { 1.0, 2.0 });
        result.RatioTrend.DataPoints.Select(dp => dp.Value).Should().BeEquivalentTo(new[] { 0.1, 0.13 });
    }

    [Fact]
    public async Task CalculateTrendAsync_WithEmptyDataPoints_ReturnsEmptyTrend()
    {
        // Arrange
        var emptyPoints = new List<TrendDataPoint>();

        // Act
        var result = await _service.CalculateTrendAsync(emptyPoints);

        // Assert
        result.Should().NotBeNull();
        result.DataPoints.Should().BeEmpty();
        result.MovingAverage.Should().BeEmpty();
        result.TrendLine.Should().BeEmpty();
        result.Summary.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateTrendAsync_WithIncreasingValues_ShowsPositiveTrend()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 10 },
            new() { Timestamp = baseTime.AddSeconds(1), Value = 20 },
            new() { Timestamp = baseTime.AddSeconds(2), Value = 30 },
            new() { Timestamp = baseTime.AddSeconds(3), Value = 40 }
        };

        // Act
        var result = await _service.CalculateTrendAsync(dataPoints, 2);

        // Assert
        result.Summary.TrendSlope.Should().BeGreaterThan(0); // Positive slope
        result.Summary.RSquared.Should().BeGreaterThan(0.9); // Good linear fit
    }

    [Fact]
    public async Task CalculateTrendAsync_WithDecreasingValues_ShowsNegativeTrend()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 40 },
            new() { Timestamp = baseTime.AddSeconds(1), Value = 30 },
            new() { Timestamp = baseTime.AddSeconds(2), Value = 20 },
            new() { Timestamp = baseTime.AddSeconds(3), Value = 10 }
        };

        // Act
        var result = await _service.CalculateTrendAsync(dataPoints, 2);

        // Assert
        result.Summary.TrendSlope.Should().BeLessThan(0); // Negative slope
        result.Summary.RSquared.Should().BeGreaterThan(0.9); // Good linear fit
    }

    [Fact]
    public async Task CalculateTrendAsync_WithConstantValues_ShowsFlatTrend()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 25 },
            new() { Timestamp = baseTime.AddSeconds(1), Value = 25 },
            new() { Timestamp = baseTime.AddSeconds(2), Value = 25 },
            new() { Timestamp = baseTime.AddSeconds(3), Value = 25 }
        };

        // Act
        var result = await _service.CalculateTrendAsync(dataPoints, 2);

        // Assert
        result.Summary.TrendSlope.Should().Be(0); // Flat trend
        result.Summary.StandardDeviation.Should().Be(0); // No variation
        result.Summary.Min.Should().Be(25);
        result.Summary.Max.Should().Be(25);
        result.Summary.Average.Should().Be(25);
    }

    [Fact]
    public async Task CalculateTrendAsync_MovingAverageCalculation_IsCorrect()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 10 },
            new() { Timestamp = baseTime.AddSeconds(1), Value = 20 },
            new() { Timestamp = baseTime.AddSeconds(2), Value = 15 },
            new() { Timestamp = baseTime.AddSeconds(3), Value = 25 },
            new() { Timestamp = baseTime.AddSeconds(4), Value = 30 }
        };

        // Act
        var result = await _service.CalculateTrendAsync(dataPoints, 3);

        // Assert
        result.MovingAverage.Should().HaveCount(5);
        result.MovingAverage[0].Value.Should().Be(10); // First point
        result.MovingAverage[1].Value.Should().Be(15); // (10+20)/2
        result.MovingAverage[2].Value.Should().Be(15); // (10+20+15)/3
        result.MovingAverage[3].Value.Should().Be(20); // (20+15+25)/3
        result.MovingAverage[4].Value.Should().BeApproximately(23.33, 0.01); // (15+25+30)/3
    }

    [Fact]
    public async Task CalculateTrendAsync_WithLargeMovingAveragePeriod_HandlesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 10 },
            new() { Timestamp = baseTime.AddSeconds(1), Value = 20 },
            new() { Timestamp = baseTime.AddSeconds(2), Value = 30 }
        };

        // Act - Moving average period larger than data points
        var result = await _service.CalculateTrendAsync(dataPoints, 10);

        // Assert
        result.MovingAverage.Should().HaveCount(3);
        result.MovingAverage[0].Value.Should().Be(10); // First point
        result.MovingAverage[1].Value.Should().Be(15); // (10+20)/2
        result.MovingAverage[2].Value.Should().Be(20); // (10+20+30)/3
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_LogsInformationForValidData()
    {
        // Arrange
        var stats = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1), new(DateTime.Now.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        // Act
        await _service.AnalyzeTrendsAsync(stats);

        // Assert
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing trends for 2 data points")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
