using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Serialization;

namespace PapyrusMonitor.Core.Export;

/// <summary>
///     Implementation of export service supporting CSV and JSON formats
/// </summary>
public class ExportService(ILogger<ExportService> logger, ISettingsService settingsService)
    : IExportService
{
    private readonly PapyrusMonitorJsonContext _jsonContext = new();

    public async Task ExportAsync(ExportData data, string filePath, ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var fileStream = File.Create(filePath);
            await ExportAsync(data, fileStream, format, cancellationToken);

            logger.LogInformation("Successfully exported data to {FilePath} in {Format} format", filePath, format);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export data to {FilePath}", filePath);
            throw;
        }
    }

    public async Task ExportAsync(ExportData data, Stream stream, ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(stream);

        switch (format)
        {
            case ExportFormat.Csv:
                await ExportToCsvAsync(data, stream, cancellationToken);
                break;
            case ExportFormat.Json:
                await ExportToJsonAsync(data, stream, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Export format {format} is not supported");
        }
    }

    public string GetFileExtension(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Csv => ".csv",
            ExportFormat.Json => ".json",
            _ => throw new NotSupportedException($"Export format {format} is not supported")
        };
    }

    private async Task ExportToCsvAsync(ExportData data, Stream stream, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
        var settings = settingsService.Settings.ExportSettings;

        // Write metadata as comments
        await writer.WriteLineAsync($"# Export Date: {data.Metadata.ExportDate:yyyy-MM-dd HH:mm:ss}");
        await writer.WriteLineAsync($"# Application Version: {data.Metadata.ApplicationVersion}");
        await writer.WriteLineAsync($"# Log File: {data.Metadata.LogFilePath}");

        if (data.Metadata.SessionStartTime.HasValue && data.Metadata.SessionEndTime.HasValue)
        {
            await writer.WriteLineAsync($"# Session Start: {data.Metadata.SessionStartTime.Value:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"# Session End: {data.Metadata.SessionEndTime.Value:yyyy-MM-dd HH:mm:ss}");
        }

        await writer.WriteLineAsync();

        // Write headers
        var headers = new List<string>
        {
            "Dumps",
            "Stacks",
            "Warnings",
            "Errors",
            "Ratio"
        };
        if (settings.IncludeTimestamps)
        {
            headers.Insert(0, "Timestamp");
        }

        await writer.WriteLineAsync(string.Join(",", headers));

        // Write data
        foreach (var stats in data.Statistics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new List<string>
            {
                stats.Dumps.ToString(),
                stats.Stacks.ToString(),
                stats.Warnings.ToString(),
                stats.Errors.ToString(),
                stats.Ratio.ToString("F2", CultureInfo.InvariantCulture)
            };

            if (settings.IncludeTimestamps)
            {
                values.Insert(0, stats.Timestamp.ToString(settings.DateFormat));
            }

            await writer.WriteLineAsync(string.Join(",", values));
        }

        // Write summary if available
        if (data.Summary != null)
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("# Summary Statistics");
            await writer.WriteLineAsync($"# Total Dumps: {data.Summary.TotalDumps}");
            await writer.WriteLineAsync($"# Total Stacks: {data.Summary.TotalStacks}");
            await writer.WriteLineAsync($"# Total Warnings: {data.Summary.TotalWarnings}");
            await writer.WriteLineAsync($"# Total Errors: {data.Summary.TotalErrors}");
            await writer.WriteLineAsync($"# Average Ratio: {data.Summary.AverageRatio:F2}");
            await writer.WriteLineAsync($"# Peak Dumps: {data.Summary.PeakDumps}");
            await writer.WriteLineAsync($"# Peak Stacks: {data.Summary.PeakStacks}");
            await writer.WriteLineAsync($"# Duration: {data.Summary.Duration:hh\\:mm\\:ss}");
        }

        await writer.FlushAsync();
    }

    private async Task ExportToJsonAsync(ExportData data, Stream stream, CancellationToken cancellationToken)
    {
        var settings = settingsService.Settings.ExportSettings;

        if (settings.IncludeTimestamps)
        {
            await JsonSerializer.SerializeAsync(stream, data, _jsonContext.ExportData, cancellationToken);
        }
        else
        {
            // Create a modified version without timestamps
            var statsWithoutTimestamps = data.Statistics.Select(s => new StatsWithoutTimestamp
            {
                Dumps = s.Dumps,
                Stacks = s.Stacks,
                Warnings = s.Warnings,
                Errors = s.Errors,
                Ratio = s.Ratio
            }).ToList();

            var exportDataWithoutTimestamps = new ExportDataWithoutTimestamps
            {
                Metadata = data.Metadata, Statistics = statsWithoutTimestamps, Summary = data.Summary
            };

            await JsonSerializer.SerializeAsync(stream, exportDataWithoutTimestamps,
                _jsonContext.ExportDataWithoutTimestamps, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }
}
