using System.Text.Json.Serialization;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Models;

namespace PapyrusMonitor.Core.Serialization;

/// <summary>
///     JSON serialization context for source generation to support trimming
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ExportSettings))]
[JsonSerializable(typeof(WindowSettings))]
[JsonSerializable(typeof(ExportData))]
[JsonSerializable(typeof(ExportMetadata))]
[JsonSerializable(typeof(SessionSummary))]
[JsonSerializable(typeof(PapyrusStats))]
[JsonSerializable(typeof(StatsWithoutTimestamp))]
[JsonSerializable(typeof(ExportDataWithoutTimestamps))]
public partial class PapyrusMonitorJsonContext : JsonSerializerContext
{
}
