using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Services;

/// <summary>
///     Service for tracking monitoring session history
/// </summary>
public interface ISessionHistoryService
{
    /// <summary>
    ///     Gets whether a session is currently active
    /// </summary>
    bool IsSessionActive { get; }

    /// <summary>
    ///     Gets the start time of the current session
    /// </summary>
    DateTime? SessionStartTime { get; }

    /// <summary>
    ///     Gets the end time of the current session
    /// </summary>
    DateTime? SessionEndTime { get; }

    /// <summary>
    ///     Starts a new monitoring session
    /// </summary>
    void StartSession();

    /// <summary>
    ///     Ends the current monitoring session
    /// </summary>
    void EndSession();

    /// <summary>
    ///     Records a statistics update
    /// </summary>
    /// <param name="stats">Statistics to record</param>
    void RecordStats(PapyrusStats stats);

    /// <summary>
    ///     Gets all statistics for the current session
    /// </summary>
    /// <returns>List of recorded statistics</returns>
    IReadOnlyList<PapyrusStats> GetSessionStatistics();

    /// <summary>
    ///     Gets the current session summary
    /// </summary>
    /// <returns>Session summary or null if no session is active</returns>
    SessionSummary? GetSessionSummary();

    /// <summary>
    ///     Clears the current session history
    /// </summary>
    void ClearHistory();
}
