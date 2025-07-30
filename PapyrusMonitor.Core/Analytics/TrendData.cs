namespace PapyrusMonitor.Core.Analytics;

/// <summary>
///     Represents trend analysis data for Papyrus statistics
/// </summary>
public record TrendData
{
    /// <summary>
    ///     Time series data points for the trend
    /// </summary>
    public IReadOnlyList<TrendDataPoint> DataPoints { get; init; } = Array.Empty<TrendDataPoint>();

    /// <summary>
    ///     Moving average data points
    /// </summary>
    public IReadOnlyList<TrendDataPoint> MovingAverage { get; init; } = Array.Empty<TrendDataPoint>();

    /// <summary>
    ///     Trend line data points (linear regression)
    /// </summary>
    public IReadOnlyList<TrendDataPoint> TrendLine { get; init; } = Array.Empty<TrendDataPoint>();

    /// <summary>
    ///     Statistical summary of the trend
    /// </summary>
    public TrendSummary Summary { get; init; } = new();
}

/// <summary>
///     Represents a single data point in a trend
/// </summary>
public record TrendDataPoint
{
    /// <summary>
    ///     Timestamp of the data point
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Value at this point
    /// </summary>
    public double Value { get; init; }
}

/// <summary>
///     Statistical summary of trend data
/// </summary>
public record TrendSummary
{
    /// <summary>
    ///     Minimum value in the trend
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    ///     Maximum value in the trend
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    ///     Average value
    /// </summary>
    public double Average { get; init; }

    /// <summary>
    ///     Standard deviation
    /// </summary>
    public double StandardDeviation { get; init; }

    /// <summary>
    ///     Trend direction (positive = increasing, negative = decreasing)
    /// </summary>
    public double TrendSlope { get; init; }

    /// <summary>
    ///     R-squared value for trend line fit
    /// </summary>
    public double RSquared { get; init; }
}

/// <summary>
///     Container for all trend analyses
/// </summary>
public record TrendAnalysisResult
{
    /// <summary>
    ///     Dumps trend data
    /// </summary>
    public TrendData DumpsTrend { get; init; } = new();

    /// <summary>
    ///     Stacks trend data
    /// </summary>
    public TrendData StacksTrend { get; init; } = new();

    /// <summary>
    ///     Warnings trend data
    /// </summary>
    public TrendData WarningsTrend { get; init; } = new();

    /// <summary>
    ///     Errors trend data
    /// </summary>
    public TrendData ErrorsTrend { get; init; } = new();

    /// <summary>
    ///     Ratio trend data
    /// </summary>
    public TrendData RatioTrend { get; init; } = new();

    /// <summary>
    ///     Time range of the analysis
    /// </summary>
    public TimeRange TimeRange { get; init; } = new();
}

/// <summary>
///     Represents a time range for analysis
/// </summary>
public record TimeRange
{
    /// <summary>
    ///     Start of the time range
    /// </summary>
    public DateTime Start { get; init; }

    /// <summary>
    ///     End of the time range
    /// </summary>
    public DateTime End { get; init; }

    /// <summary>
    ///     Duration of the time range
    /// </summary>
    public TimeSpan Duration
    {
        get => End - Start;
    }
}
