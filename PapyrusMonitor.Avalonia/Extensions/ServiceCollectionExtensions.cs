using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Avalonia.Services;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Avalonia.Extensions;

/// <summary>
///     Extension methods for configuring PapyrusMonitor Avalonia services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds PapyrusMonitor Avalonia ViewModels to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPapyrusMonitorViewModels(this IServiceCollection services)
    {
        // Register services for testability
        services.AddSingleton<ISchedulerProvider, AvaloniaSchedulerProvider>();
        services.AddSingleton<ILogger, ConsoleLogger>();

        // Register ViewModels as transient - each request gets a new instance
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<PapyrusMonitorViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
