using PapyrusMonitor.Core.Export;

namespace PapyrusMonitor.Core.Interfaces;

/// <summary>
///     Service for exporting monitoring data
/// </summary>
public interface IExportService
{
    /// <summary>
    ///     Exports data to a file in the specified format
    /// </summary>
    /// <param name="data">Data to export</param>
    /// <param name="filePath">Path to the output file</param>
    /// <param name="format">Export format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when export is finished</returns>
    Task ExportAsync(ExportData data, string filePath, ExportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports data to a stream in the specified format
    /// </summary>
    /// <param name="data">Data to export</param>
    /// <param name="stream">Output stream</param>
    /// <param name="format">Export format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when export is finished</returns>
    Task ExportAsync(ExportData data, Stream stream, ExportFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the file extension for the specified format
    /// </summary>
    /// <param name="format">Export format</param>
    /// <returns>File extension including the dot (e.g., ".csv")</returns>
    string GetFileExtension(ExportFormat format);
}
