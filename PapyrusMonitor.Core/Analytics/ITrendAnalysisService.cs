using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Analytics;

/// <summary>
/// Service for analyzing trends in Papyrus statistics
/// </summary>
public interface ITrendAnalysisService
{
    /// <summary>
    /// Analyzes trends in the provided statistics data
    /// </summary>
    /// <param name="statistics">Collection of statistics to analyze</param>
    /// <param name="movingAveragePeriod">Period for moving average calculation (default: 5)</param>
    /// <returns>Trend analysis result</returns>
    Task<TrendAnalysisResult> AnalyzeTrendsAsync(
        IReadOnlyList<PapyrusStats> statistics,
        int movingAveragePeriod = 5);
    
    /// <summary>
    /// Calculates trend data for a specific metric
    /// </summary>
    /// <param name="dataPoints">Raw data points</param>
    /// <param name="movingAveragePeriod">Period for moving average</param>
    /// <returns>Trend data with calculations</returns>
    Task<TrendData> CalculateTrendAsync(
        IEnumerable<TrendDataPoint> dataPoints,
        int movingAveragePeriod = 5);
}