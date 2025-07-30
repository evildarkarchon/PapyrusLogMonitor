using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PapyrusMonitor.Avalonia.Extensions;
using PapyrusMonitor.Avalonia.Services;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Extensions;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Avalonia.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPapyrusMonitorViewModels_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPapyrusMonitorCore(); // Add core services that ViewModels depend on

        // Act
        services.AddPapyrusMonitorViewModels();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Services
        serviceProvider.GetService<ISchedulerProvider>().Should().NotBeNull().And.BeOfType<AvaloniaSchedulerProvider>();
        serviceProvider.GetService<Core.Interfaces.ILogger>().Should().NotBeNull().And.BeOfType<ConsoleLogger>();
        
        // Assert - ViewModels
        serviceProvider.GetService<MainWindowViewModel>().Should().NotBeNull();
        serviceProvider.GetService<PapyrusMonitorViewModel>().Should().NotBeNull();
        serviceProvider.GetService<SettingsViewModel>().Should().NotBeNull();
    }

    [Fact]
    public void AddPapyrusMonitorViewModels_RegistersServicesAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPapyrusMonitorCore();

        // Act
        services.AddPapyrusMonitorViewModels();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Services should be singletons
        var scheduler1 = serviceProvider.GetService<ISchedulerProvider>();
        var scheduler2 = serviceProvider.GetService<ISchedulerProvider>();
        scheduler1.Should().BeSameAs(scheduler2);

        var logger1 = serviceProvider.GetService<Core.Interfaces.ILogger>();
        var logger2 = serviceProvider.GetService<Core.Interfaces.ILogger>();
        logger1.Should().BeSameAs(logger2);
    }

    [Fact]
    public void AddPapyrusMonitorViewModels_RegistersViewModelsAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add required dependencies
        services.AddPapyrusMonitorCore(); // Add core services that ViewModels depend on

        // Act
        services.AddPapyrusMonitorViewModels();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - ViewModels should be transient (new instance each time)
        var mainWindow1 = serviceProvider.GetService<MainWindowViewModel>();
        var mainWindow2 = serviceProvider.GetService<MainWindowViewModel>();
        mainWindow1.Should().NotBeSameAs(mainWindow2);

        var monitor1 = serviceProvider.GetService<PapyrusMonitorViewModel>();
        var monitor2 = serviceProvider.GetService<PapyrusMonitorViewModel>();
        monitor1.Should().NotBeSameAs(monitor2);

        var settings1 = serviceProvider.GetService<SettingsViewModel>();
        var settings2 = serviceProvider.GetService<SettingsViewModel>();
        settings1.Should().NotBeSameAs(settings2);
    }

    [Fact]
    public void AddPapyrusMonitorViewModels_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddPapyrusMonitorViewModels();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Theory]
    [InlineData(typeof(ISchedulerProvider), typeof(AvaloniaSchedulerProvider), ServiceLifetime.Singleton)]
    [InlineData(typeof(Core.Interfaces.ILogger), typeof(ConsoleLogger), ServiceLifetime.Singleton)]
    [InlineData(typeof(MainWindowViewModel), typeof(MainWindowViewModel), ServiceLifetime.Transient)]
    [InlineData(typeof(PapyrusMonitorViewModel), typeof(PapyrusMonitorViewModel), ServiceLifetime.Transient)]
    [InlineData(typeof(SettingsViewModel), typeof(SettingsViewModel), ServiceLifetime.Transient)]
    public void AddPapyrusMonitorViewModels_RegistersWithCorrectLifetime(Type serviceType, Type implementationType, ServiceLifetime expectedLifetime)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPapyrusMonitorViewModels();

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == serviceType);
        descriptor.Should().NotBeNull();
        if (serviceType == implementationType)
        {
            // For ViewModels registered as themselves
            descriptor!.ImplementationType.Should().Be(implementationType);
        }
        else
        {
            // For interfaces
            descriptor!.ImplementationType.Should().Be(implementationType);
        }
        descriptor.Lifetime.Should().Be(expectedLifetime);
    }

    [Fact]
    public void AddPapyrusMonitorViewModels_CanResolveViewModelsWithDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging
        services.AddPapyrusMonitorCore(); // Add core services

        // Act
        services.AddPapyrusMonitorViewModels();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should be able to create ViewModels with all dependencies
        var mainWindowAction = () => serviceProvider.GetService<MainWindowViewModel>();
        mainWindowAction.Should().NotThrow();

        var monitorAction = () => serviceProvider.GetService<PapyrusMonitorViewModel>();
        monitorAction.Should().NotThrow();

        var settingsAction = () => serviceProvider.GetService<SettingsViewModel>();
        settingsAction.Should().NotThrow();
    }

    [Fact]
    public void AddPapyrusMonitorViewModels_MultipleCalls_CreatesMultipleRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPapyrusMonitorViewModels();
        services.AddPapyrusMonitorViewModels(); // Call twice
        
        // Assert - Currently allows duplicate registrations
        var schedulerDescriptors = services.Where(s => s.ServiceType == typeof(ISchedulerProvider)).ToList();
        schedulerDescriptors.Should().HaveCount(2);
        
        var viewModelDescriptors = services.Where(s => s.ServiceType == typeof(MainWindowViewModel)).ToList();
        viewModelDescriptors.Should().HaveCount(2);
        
        // Note: This is the current behavior. If you want to prevent duplicates,
        // the implementation would need to check if services are already registered
    }

    [Fact]
    public void ServiceRegistration_FullIntegration_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act - Register both Core and Avalonia services
        services.AddPapyrusMonitorCore()
                .AddPapyrusMonitorViewModels();
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Can resolve a ViewModel with all its dependencies
        var mainWindowViewModel = serviceProvider.GetService<MainWindowViewModel>();
        mainWindowViewModel.Should().NotBeNull();

        // Verify the ViewModel has access to core services through DI
        var monitorService = serviceProvider.GetService<IPapyrusMonitorService>();
        monitorService.Should().NotBeNull();
        
        var scheduler = serviceProvider.GetService<ISchedulerProvider>();
        scheduler.Should().NotBeNull();
    }
}