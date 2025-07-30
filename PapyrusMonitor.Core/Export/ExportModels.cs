namespace PapyrusMonitor.Core.Export;

/// <summary>
///     Statistics without timestamps for export
/// </summary>
public record StatsWithoutTimestamp
{
    public int Dumps { get; init; }
    public int Stacks { get; init; }
    public int Warnings { get; init; }
    public int Errors { get; init; }
    public double Ratio { get; init; }
}

/// <summary>
///     Export data without timestamps
/// </summary>
public record ExportDataWithoutTimestamps
{
    public ExportMetadata Metadata { get; init; } = new();
    public IReadOnlyList<StatsWithoutTimestamp> Statistics { get; init; } = Array.Empty<StatsWithoutTimestamp>();
    public SessionSummary? Summary { get; init; }
}
