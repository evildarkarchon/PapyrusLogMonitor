using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Avalonia.ViewModels;

namespace PapyrusMonitor.Avalonia.Extensions;

/// <summary>
/// Extension methods for configuring PapyrusMonitor Avalonia services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PapyrusMonitor Avalonia ViewModels to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPapyrusMonitorViewModels(this IServiceCollection services)
    {
        // Register ViewModels as transient - each request gets a new instance
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<PapyrusMonitorViewModel>();
        
        return services;
    }
}