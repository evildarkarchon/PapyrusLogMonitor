using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Extensions;

/// <summary>
///     Extension methods for configuring PapyrusMonitor Core services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds PapyrusMonitor Core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">Optional monitoring configuration. If null, uses default configuration.</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPapyrusMonitorCore(
        this IServiceCollection services,
        MonitoringConfiguration? configuration = null)
    {
        // Register file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();

        // Register core services
        services.AddSingleton<ILogParser, PapyrusLogParser>();
        services.AddSingleton<IFileWatcher, FileWatcher>();
        services.AddSingleton<IFileTailReader, FileTailReader>();
        services.AddSingleton<IPapyrusMonitorService, PapyrusMonitorService>();

        // Register new services for Phase 6
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<ISessionHistoryService, SessionHistoryService>();
        services.AddSingleton<ITrendAnalysisService, TrendAnalysisService>();

        // Register configuration
        var config = configuration ?? CreateDefaultConfiguration();
        services.AddSingleton(config);

        return services;
    }

    /// <summary>
    ///     Adds PapyrusMonitor Core services to the service collection with a configuration builder.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureOptions">Action to configure the monitoring options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPapyrusMonitorCore(
        this IServiceCollection services,
        Action<MonitoringConfiguration> configureOptions)
    {
        var configuration = CreateDefaultConfiguration();
        configureOptions(configuration);

        return services.AddPapyrusMonitorCore(configuration);
    }

    /// <summary>
    ///     Creates a default monitoring configuration.
    /// </summary>
    /// <returns>A default MonitoringConfiguration instance</returns>
    private static MonitoringConfiguration CreateDefaultConfiguration()
    {
        return new MonitoringConfiguration
        {
            LogFilePath = GetDefaultLogPath(), UpdateIntervalMs = 1000, UseFileWatcher = true
        };
    }

    /// <summary>
    ///     Gets the default Papyrus log file path for the current user.
    /// </summary>
    /// <returns>The default log file path</returns>
    private static string GetDefaultLogPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Try Fallout 4 first, then Skyrim as fallback
        var fallout4Path = Path.Combine(userProfile, "Documents", "My Games", "Fallout4", "Logs", "Script",
            "Papyrus.0.log");
        var skyrimPath = Path.Combine(userProfile, "Documents", "My Games", "Skyrim", "Logs", "Script",
            "Papyrus.0.log");

        // Return the first one that exists, or Fallout 4 path as default
        return File.Exists(fallout4Path) ? fallout4Path :
            File.Exists(skyrimPath) ? skyrimPath :
            fallout4Path;
    }
}
