using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Services;

public class SessionHistoryServiceTests
{
    private readonly Mock<ILogger<SessionHistoryService>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly SessionHistoryService _service;
    private readonly AppSettings _testSettings;

    public SessionHistoryServiceTests()
    {
        _mockLogger = new Mock<ILogger<SessionHistoryService>>();
        _mockSettingsService = new Mock<ISettingsService>();

        _testSettings = new AppSettings { MaxLogEntries = 1000 };
        _mockSettingsService.Setup(x => x.Settings).Returns(_testSettings);

        _service = new SessionHistoryService(_mockLogger.Object, _mockSettingsService.Object);
    }

    [Fact]
    public void StartSession_SetsSessionActiveAndStartTime()
    {
        // Arrange
        var beforeStart = DateTime.Now;

        // Act
        _service.StartSession();
        var afterStart = DateTime.Now;

        // Assert
        _service.IsSessionActive.Should().BeTrue();
        _service.SessionStartTime.Should().NotBeNull();
        _service.SessionStartTime.Value.Should().BeOnOrAfter(beforeStart);
        _service.SessionStartTime.Value.Should().BeOnOrBefore(afterStart);
        _service.SessionEndTime.Should().BeNull();
    }

    [Fact]
    public void StartSession_WhenAlreadyActive_LogsWarningAndDoesNothing()
    {
        // Arrange
        _service.StartSession();
        var originalStartTime = _service.SessionStartTime;

        // Act
        _service.StartSession();

        // Assert
        _service.SessionStartTime.Should().Be(originalStartTime);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already active")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void EndSession_SetsSessionInactiveAndEndTime()
    {
        // Arrange
        _service.StartSession();
        var beforeEnd = DateTime.Now;

        // Act
        _service.EndSession();
        var afterEnd = DateTime.Now;

        // Assert
        _service.IsSessionActive.Should().BeFalse();
        _service.SessionEndTime.Should().NotBeNull();
        _service.SessionEndTime.Value.Should().BeOnOrAfter(beforeEnd);
        _service.SessionEndTime.Value.Should().BeOnOrBefore(afterEnd);
    }

    [Fact]
    public void EndSession_WhenNotActive_LogsWarningAndDoesNothing()
    {
        // Act
        _service.EndSession();

        // Assert
        _service.IsSessionActive.Should().BeFalse();
        _service.SessionEndTime.Should().BeNull();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("none is active")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void RecordStats_WhenSessionActive_AddsStatsToHistory()
    {
        // Arrange
        _service.StartSession();
        var stats = new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1);

        // Act
        _service.RecordStats(stats);

        // Assert
        var sessionStats = _service.GetSessionStatistics();
        sessionStats.Should().HaveCount(1);
        sessionStats[0].Should().Be(stats);
    }

    [Fact]
    public void RecordStats_WhenSessionInactive_IgnoresStats()
    {
        // Arrange
        var stats = new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1);

        // Act
        _service.RecordStats(stats);

        // Assert
        var sessionStats = _service.GetSessionStatistics();
        sessionStats.Should().BeEmpty();
        _mockLogger.Verify(x => x.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("no session is active")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void RecordStats_WithNullStats_ThrowsArgumentNullException()
    {
        // Arrange
        _service.StartSession();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.RecordStats(null!));
    }

    [Fact]
    public void RecordStats_WhenExceedsMaxEntries_TrimsOldestEntries()
    {
        // Arrange
        var smallSettings = new AppSettings { MaxLogEntries = 5 };
        var mockSettingsService = new Mock<ISettingsService>();
        mockSettingsService.Setup(x => x.Settings).Returns(smallSettings);
        var service = new SessionHistoryService(_mockLogger.Object, mockSettingsService.Object);
        service.StartSession();

        // Add 7 stats entries
        for (var i = 0; i < 7; i++)
        {
            var stats = new PapyrusStats(DateTime.Now.AddMinutes(i), i, i, i, i, i * 0.1);
            service.RecordStats(stats);
        }

        // Act
        var sessionStats = service.GetSessionStatistics();

        // Assert
        sessionStats.Should().HaveCount(5);
        sessionStats.First().Dumps.Should().Be(2); // Oldest two should be removed
        sessionStats.Last().Dumps.Should().Be(6);
    }

    [Fact]
    public void GetSessionStatistics_ReturnsStatsOrderedByTimestamp()
    {
        // Arrange
        _service.StartSession();
        var stats1 = new PapyrusStats(DateTime.Now.AddMinutes(2), 20, 10, 4, 2, 0.2);
        var stats2 = new PapyrusStats(DateTime.Now.AddMinutes(1), 10, 5, 2, 1, 0.1);
        var stats3 = new PapyrusStats(DateTime.Now.AddMinutes(3), 30, 15, 6, 3, 0.3);

        _service.RecordStats(stats1);
        _service.RecordStats(stats2);
        _service.RecordStats(stats3);

        // Act
        var sessionStats = _service.GetSessionStatistics();

        // Assert
        sessionStats.Should().HaveCount(3);
        sessionStats[0].Should().Be(stats2); // Earliest
        sessionStats[1].Should().Be(stats1);
        sessionStats[2].Should().Be(stats3); // Latest
    }

    [Fact]
    public void GetSessionSummary_WithNoStats_ReturnsNull()
    {
        // Arrange
        _service.StartSession();

        // Act
        var summary = _service.GetSessionSummary();

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public void GetSessionSummary_WithStats_ReturnsCorrectSummary()
    {
        // Arrange
        _service.StartSession();
        var stats1 = new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1);
        var stats2 = new PapyrusStats(DateTime.Now.AddMinutes(1), 15, 8, 3, 2, 0.13);
        var stats3 = new PapyrusStats(DateTime.Now.AddMinutes(2), 20, 10, 4, 3, 0.15);

        _service.RecordStats(stats1);
        _service.RecordStats(stats2);
        _service.RecordStats(stats3);

        // Act
        var summary = _service.GetSessionSummary();

        // Assert
        summary.Should().NotBeNull();
        summary!.TotalDumps.Should().Be(45); // 10 + 15 + 20
        summary.TotalStacks.Should().Be(23); // 5 + 8 + 10
        summary.TotalWarnings.Should().Be(9); // 2 + 3 + 4
        summary.TotalErrors.Should().Be(6); // 1 + 2 + 3
        summary.AverageRatio.Should().BeApproximately(0.127, 0.001); // (0.1 + 0.13 + 0.15) / 3
        summary.PeakDumps.Should().Be(20);
        summary.PeakStacks.Should().Be(10);
        summary.Duration.Should().BePositive();
    }

    [Fact]
    public void GetSessionSummary_WithEndedSession_UsesSavedEndTime()
    {
        // Arrange
        _service.StartSession();
        var stats = new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1);
        _service.RecordStats(stats);
        _service.EndSession();
        var endTime = _service.SessionEndTime;

        // Act
        var summary = _service.GetSessionSummary();

        // Assert
        summary.Should().NotBeNull();
        summary!.Duration.Should().Be(endTime!.Value - _service.SessionStartTime!.Value);
    }

    [Fact]
    public void GetSessionSummary_WithoutStartTime_ReturnsNull()
    {
        // Arrange - Service without started session
        var freshService = new SessionHistoryService(_mockLogger.Object, _mockSettingsService.Object);

        // Act
        var summary = freshService.GetSessionSummary();

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public void ClearHistory_RemovesAllStats()
    {
        // Arrange
        _service.StartSession();
        _service.RecordStats(new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1));
        _service.RecordStats(new PapyrusStats(DateTime.Now, 15, 8, 3, 2, 0.13));

        // Act
        _service.ClearHistory();

        // Assert
        var sessionStats = _service.GetSessionStatistics();
        sessionStats.Should().BeEmpty();
    }

    [Fact]
    public void StartSession_ClearsExistingStats()
    {
        // Arrange
        _service.StartSession();
        _service.RecordStats(new PapyrusStats(DateTime.Now, 10, 5, 2, 1, 0.1));
        _service.EndSession();

        // Act
        _service.StartSession();

        // Assert
        var sessionStats = _service.GetSessionStatistics();
        sessionStats.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentOperations_MaintainsThreadSafety()
    {
        // Arrange
        _service.StartSession();
        var tasks = new List<Task>();

        // Act - Concurrent writes
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var stats = new PapyrusStats(DateTime.Now.AddSeconds(index), index, index, index, index, index * 0.1);
                _service.RecordStats(stats);
            }));
        }

        // Concurrent reads
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var stats = _service.GetSessionStatistics();
                var summary = _service.GetSessionSummary();
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        var finalStats = _service.GetSessionStatistics();
        finalStats.Should().HaveCount(10);
        finalStats.Should().BeInAscendingOrder(s => s.Timestamp);
    }
}
