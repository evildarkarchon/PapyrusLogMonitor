using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Avalonia.Views;

namespace PapyrusMonitor.Avalonia;

public partial class App : Application
{
    /// <summary>
    /// Gets or sets the service provider for dependency injection.
    /// This is set by the Program.cs during application startup.
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            
            // Create MainWindowViewModel with storage provider
            var papyrusMonitorViewModel = ServiceProvider.GetRequiredService<PapyrusMonitorViewModel>();
            var settingsService = ServiceProvider.GetRequiredService<PapyrusMonitor.Core.Configuration.ISettingsService>();
            var exportService = ServiceProvider.GetRequiredService<PapyrusMonitor.Core.Export.IExportService>();
            var sessionHistoryService = ServiceProvider.GetRequiredService<PapyrusMonitor.Core.Services.ISessionHistoryService>();
            var trendAnalysisService = ServiceProvider.GetRequiredService<PapyrusMonitor.Core.Analytics.ITrendAnalysisService>();
            
            var mainWindowViewModel = new MainWindowViewModel(
                papyrusMonitorViewModel,
                settingsService,
                exportService,
                sessionHistoryService,
                trendAnalysisService,
                mainWindow.StorageProvider);
            
            mainWindow.DataContext = mainWindowViewModel;
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = ServiceProvider.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
