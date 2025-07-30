using Microsoft.Extensions.Logging;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Analytics;

/// <summary>
///     Implementation of trend analysis service
/// </summary>
public class TrendAnalysisService : ITrendAnalysisService
{
    private readonly ILogger<TrendAnalysisService> _logger;

    public TrendAnalysisService(ILogger<TrendAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<TrendAnalysisResult> AnalyzeTrendsAsync(
        IReadOnlyList<PapyrusStats> statistics,
        int movingAveragePeriod = 5)
    {
        if (statistics == null || statistics.Count == 0)
        {
            _logger.LogWarning("No statistics provided for trend analysis");
            return new TrendAnalysisResult();
        }

        _logger.LogInformation("Analyzing trends for {Count} data points", statistics.Count);

        // Extract data points for each metric
        var dumpsPoints = statistics.Select(s => new TrendDataPoint { Timestamp = s.Timestamp, Value = s.Dumps })
            .ToList();

        var stacksPoints = statistics.Select(s => new TrendDataPoint { Timestamp = s.Timestamp, Value = s.Stacks })
            .ToList();

        var warningsPoints = statistics.Select(s => new TrendDataPoint { Timestamp = s.Timestamp, Value = s.Warnings })
            .ToList();

        var errorsPoints = statistics.Select(s => new TrendDataPoint { Timestamp = s.Timestamp, Value = s.Errors })
            .ToList();

        var ratioPoints = statistics.Select(s => new TrendDataPoint { Timestamp = s.Timestamp, Value = s.Ratio })
            .ToList();

        // Calculate trends for each metric in parallel
        var tasks = new[]
        {
            CalculateTrendAsync(dumpsPoints, movingAveragePeriod),
            CalculateTrendAsync(stacksPoints, movingAveragePeriod),
            CalculateTrendAsync(warningsPoints, movingAveragePeriod),
            CalculateTrendAsync(errorsPoints, movingAveragePeriod),
            CalculateTrendAsync(ratioPoints, movingAveragePeriod)
        };

        var results = await Task.WhenAll(tasks);

        var timeRange = new TimeRange { Start = statistics.First().Timestamp, End = statistics.Last().Timestamp };

        return new TrendAnalysisResult
        {
            DumpsTrend = results[0],
            StacksTrend = results[1],
            WarningsTrend = results[2],
            ErrorsTrend = results[3],
            RatioTrend = results[4],
            TimeRange = timeRange
        };
    }

    public async Task<TrendData> CalculateTrendAsync(
        IEnumerable<TrendDataPoint> dataPoints,
        int movingAveragePeriod = 5)
    {
        var points = dataPoints.ToList();
        if (points.Count == 0)
        {
            return new TrendData();
        }

        return await Task.Run(() =>
        {
            var movingAverage = CalculateMovingAverage(points, movingAveragePeriod);
            var (trendLine, slope, rSquared) = CalculateLinearRegression(points);
            var summary = CalculateSummary(points, slope, rSquared);

            return new TrendData
            {
                DataPoints = points, MovingAverage = movingAverage, TrendLine = trendLine, Summary = summary
            };
        });
    }

    private List<TrendDataPoint> CalculateMovingAverage(
        List<TrendDataPoint> points,
        int period)
    {
        var movingAverage = new List<TrendDataPoint>();

        for (var i = 0; i < points.Count; i++)
        {
            var startIndex = Math.Max(0, i - period + 1);
            var count = i - startIndex + 1;

            double sum = 0;
            for (var j = startIndex; j <= i; j++)
            {
                sum += points[j].Value;
            }

            movingAverage.Add(new TrendDataPoint { Timestamp = points[i].Timestamp, Value = sum / count });
        }

        return movingAverage;
    }

    private (List<TrendDataPoint> trendLine, double slope, double rSquared) CalculateLinearRegression(
        List<TrendDataPoint> points)
    {
        if (points.Count < 2)
        {
            return (points, 0, 0);
        }

        // Convert timestamps to numeric values (seconds from first point)
        var firstTime = points[0].Timestamp;
        var xValues = points.Select(p => (p.Timestamp - firstTime).TotalSeconds).ToArray();
        var yValues = points.Select(p => p.Value).ToArray();

        // Calculate means
        var xMean = xValues.Average();
        var yMean = yValues.Average();

        // Calculate slope and intercept
        double numerator = 0;
        double denominator = 0;

        for (var i = 0; i < xValues.Length; i++)
        {
            numerator += (xValues[i] - xMean) * (yValues[i] - yMean);
            denominator += Math.Pow(xValues[i] - xMean, 2);
        }

        var slope = denominator != 0 ? numerator / denominator : 0;
        var intercept = yMean - slope * xMean;

        // Calculate R-squared
        double ssTotal = 0;
        double ssResidual = 0;

        for (var i = 0; i < yValues.Length; i++)
        {
            var yPredicted = slope * xValues[i] + intercept;
            ssTotal += Math.Pow(yValues[i] - yMean, 2);
            ssResidual += Math.Pow(yValues[i] - yPredicted, 2);
        }

        var rSquared = ssTotal != 0 ? 1 - ssResidual / ssTotal : 0;

        // Generate trend line points
        var trendLine = points.Select((p, i) => new TrendDataPoint
        {
            Timestamp = p.Timestamp, Value = slope * xValues[i] + intercept
        }).ToList();

        return (trendLine, slope, rSquared);
    }

    private TrendSummary CalculateSummary(
        List<TrendDataPoint> points,
        double slope,
        double rSquared)
    {
        var values = points.Select(p => p.Value).ToList();

        var min = values.Min();
        var max = values.Max();
        var average = values.Average();

        // Calculate standard deviation
        var sumSquaredDiff = values.Sum(v => Math.Pow(v - average, 2));
        var standardDeviation = Math.Sqrt(sumSquaredDiff / values.Count);

        return new TrendSummary
        {
            Min = min,
            Max = max,
            Average = average,
            StandardDeviation = standardDeviation,
            TrendSlope = slope,
            RSquared = rSquared
        };
    }
}
