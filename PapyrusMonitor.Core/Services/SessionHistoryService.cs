using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Services;

/// <summary>
///     Implementation of session history tracking service
/// </summary>
public class SessionHistoryService : ISessionHistoryService
{
    private readonly ILogger<SessionHistoryService> _logger;
    private readonly object _sessionLock = new();
    private readonly ISettingsService _settingsService;
    private readonly ConcurrentBag<PapyrusStats> _statistics;
    private bool _isSessionActive;
    private DateTime? _sessionEndTime;

    private DateTime? _sessionStartTime;

    public SessionHistoryService(ILogger<SessionHistoryService> logger, ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _statistics = new ConcurrentBag<PapyrusStats>();
    }

    public bool IsSessionActive
    {
        get
        {
            lock (_sessionLock)
            {
                return _isSessionActive;
            }
        }
    }

    public DateTime? SessionStartTime
    {
        get
        {
            lock (_sessionLock)
            {
                return _sessionStartTime;
            }
        }
    }

    public DateTime? SessionEndTime
    {
        get
        {
            lock (_sessionLock)
            {
                return _sessionEndTime;
            }
        }
    }

    public void StartSession()
    {
        lock (_sessionLock)
        {
            if (_isSessionActive)
            {
                _logger.LogWarning("Attempted to start a session while one is already active");
                return;
            }

            _sessionStartTime = DateTime.Now;
            _sessionEndTime = null;
            _isSessionActive = true;
            _statistics.Clear();

            _logger.LogInformation("Started new monitoring session at {StartTime}", _sessionStartTime);
        }
    }

    public void EndSession()
    {
        lock (_sessionLock)
        {
            if (!_isSessionActive)
            {
                _logger.LogWarning("Attempted to end a session while none is active");
                return;
            }

            _sessionEndTime = DateTime.Now;
            _isSessionActive = false;

            _logger.LogInformation("Ended monitoring session at {EndTime}", _sessionEndTime);
        }
    }

    public void RecordStats(PapyrusStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        if (!IsSessionActive)
        {
            _logger.LogDebug("Stats recorded while no session is active, ignoring");
            return;
        }

        _statistics.Add(stats);

        // Trim history if it exceeds the maximum
        var maxEntries = _settingsService.Settings.MaxLogEntries;
        if (_statistics.Count > maxEntries)
        {
            var orderedStats = _statistics.OrderBy(s => s.Timestamp).ToList();
            var toKeep = orderedStats.Skip(orderedStats.Count - maxEntries).ToList();

            _statistics.Clear();
            foreach (var stat in toKeep)
            {
                _statistics.Add(stat);
            }

            _logger.LogDebug("Trimmed session history to {MaxEntries} entries", maxEntries);
        }
    }

    public IReadOnlyList<PapyrusStats> GetSessionStatistics()
    {
        return _statistics.OrderBy(s => s.Timestamp).ToList();
    }

    public SessionSummary? GetSessionSummary()
    {
        var statistics = GetSessionStatistics();
        if (!statistics.Any())
        {
            return null;
        }

        DateTime startTime, endTime;
        lock (_sessionLock)
        {
            if (!_sessionStartTime.HasValue)
            {
                return null;
            }

            startTime = _sessionStartTime.Value;
            endTime = _sessionEndTime ?? DateTime.Now;
        }

        var totalDumps = statistics.Sum(s => s.Dumps);
        var totalStacks = statistics.Sum(s => s.Stacks);
        var totalWarnings = statistics.Sum(s => s.Warnings);
        var totalErrors = statistics.Sum(s => s.Errors);
        var averageRatio = statistics.Average(s => s.Ratio);
        var peakDumps = statistics.Max(s => s.Dumps);
        var peakStacks = statistics.Max(s => s.Stacks);
        var duration = endTime - startTime;

        return new SessionSummary
        {
            TotalDumps = totalDumps,
            TotalStacks = totalStacks,
            TotalWarnings = totalWarnings,
            TotalErrors = totalErrors,
            AverageRatio = averageRatio,
            PeakDumps = peakDumps,
            PeakStacks = peakStacks,
            Duration = duration
        };
    }

    public void ClearHistory()
    {
        _statistics.Clear();
        _logger.LogInformation("Cleared session history");
    }
}
