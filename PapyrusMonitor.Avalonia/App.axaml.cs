using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Avalonia.Views;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Avalonia;

public class App : Application
{
    /// <summary>
    ///     Gets or sets the service provider for dependency injection.
    ///     This is set by the Program.cs during application startup.
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("ServiceProvider must be set before framework initialization.");
        }

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
            {
                var mainWindow = new MainWindow();

                // Create MainWindowViewModel with storage provider
                var papyrusMonitorViewModel = ServiceProvider.GetRequiredService<PapyrusMonitorViewModel>();
                var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
                var exportService = ServiceProvider.GetRequiredService<IExportService>();
                var sessionHistoryService = ServiceProvider.GetRequiredService<ISessionHistoryService>();
                var trendAnalysisService = ServiceProvider.GetRequiredService<ITrendAnalysisService>();
                var schedulerProvider = ServiceProvider.GetRequiredService<ISchedulerProvider>();
                var logger = ServiceProvider.GetRequiredService<ILogger>();

                var mainWindowViewModel = new MainWindowViewModel(
                    papyrusMonitorViewModel,
                    settingsService,
                    exportService,
                    sessionHistoryService,
                    trendAnalysisService,
                    schedulerProvider,
                    logger,
                    mainWindow.StorageProvider);

                mainWindow.DataContext = mainWindowViewModel;
                desktop.MainWindow = mainWindow;
                break;
            }
            case ISingleViewApplicationLifetime:
                throw new NotSupportedException(
                    "Single view platform is not supported. This application requires a desktop environment.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
