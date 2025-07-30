using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Extensions;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;

namespace PapyrusMonitor.Core.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPapyrusMonitorCore_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging services

        // Act
        services.AddPapyrusMonitorCore();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - File system abstraction
        serviceProvider.GetService<IFileSystem>().Should().NotBeNull().And.BeOfType<FileSystem>();

        // Assert - Core services
        serviceProvider.GetService<ILogParser>().Should().NotBeNull().And.BeOfType<PapyrusLogParser>();
        serviceProvider.GetService<IFileWatcher>().Should().NotBeNull().And.BeOfType<FileWatcher>();
        serviceProvider.GetService<IFileTailReader>().Should().NotBeNull().And.BeOfType<FileTailReader>();
        serviceProvider.GetService<IPapyrusMonitorService>().Should().NotBeNull().And.BeOfType<PapyrusMonitorService>();

        // Assert - Phase 6 services
        serviceProvider.GetService<ISettingsService>().Should().NotBeNull().And.BeOfType<JsonSettingsService>();
        serviceProvider.GetService<IExportService>().Should().NotBeNull().And.BeOfType<ExportService>();
        serviceProvider.GetService<ISessionHistoryService>().Should().NotBeNull().And.BeOfType<SessionHistoryService>();
        serviceProvider.GetService<ITrendAnalysisService>().Should().NotBeNull().And.BeOfType<TrendAnalysisService>();

        // Assert - Configuration
        serviceProvider.GetService<MonitoringConfiguration>().Should().NotBeNull();
    }

    [Fact]
    public void AddPapyrusMonitorCore_RegistersServicesAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPapyrusMonitorCore();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Get services twice and verify they're the same instance
        var fileSystem1 = serviceProvider.GetService<IFileSystem>();
        var fileSystem2 = serviceProvider.GetService<IFileSystem>();
        fileSystem1.Should().BeSameAs(fileSystem2);

        var logParser1 = serviceProvider.GetService<ILogParser>();
        var logParser2 = serviceProvider.GetService<ILogParser>();
        logParser1.Should().BeSameAs(logParser2);

        var monitorService1 = serviceProvider.GetService<IPapyrusMonitorService>();
        var monitorService2 = serviceProvider.GetService<IPapyrusMonitorService>();
        monitorService1.Should().BeSameAs(monitorService2);
    }

    [Fact]
    public void AddPapyrusMonitorCore_WithCustomConfiguration_UsesProvidedConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var customConfig = new MonitoringConfiguration
        {
            LogFilePath = "/custom/path/to/log.txt", UpdateIntervalMs = 5000, UseFileWatcher = false
        };

        // Act
        services.AddPapyrusMonitorCore(customConfig);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredConfig = serviceProvider.GetService<MonitoringConfiguration>();
        registeredConfig.Should().NotBeNull();
        registeredConfig.Should().BeSameAs(customConfig);
        registeredConfig!.LogFilePath.Should().Be("/custom/path/to/log.txt");
        registeredConfig.UpdateIntervalMs.Should().Be(5000);
        registeredConfig.UseFileWatcher.Should().BeFalse();
    }

    [Fact]
    public void AddPapyrusMonitorCore_WithConfigurationBuilder_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPapyrusMonitorCore(config =>
        {
            config.LogFilePath = "/configured/path/log.txt";
            config.UpdateIntervalMs = 2500;
            config.UseFileWatcher = false;
        });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredConfig = serviceProvider.GetService<MonitoringConfiguration>();
        registeredConfig.Should().NotBeNull();
        registeredConfig!.LogFilePath.Should().Be("/configured/path/log.txt");
        registeredConfig.UpdateIntervalMs.Should().Be(2500);
        registeredConfig.UseFileWatcher.Should().BeFalse();
    }

    [Fact]
    public void AddPapyrusMonitorCore_WithoutConfiguration_UsesDefaultConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddPapyrusMonitorCore();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var config = serviceProvider.GetService<MonitoringConfiguration>();
        config.Should().NotBeNull();
        config!.UpdateIntervalMs.Should().Be(1000);
        config.UseFileWatcher.Should().BeTrue();
        config.LogFilePath.Should().NotBeNullOrEmpty();
        // Path should contain expected game folder structure
        config.LogFilePath.Should().Match(path =>
            path.Contains("Fallout4") || path.Contains("Skyrim"));
        config.LogFilePath.Should().EndWith("Papyrus.0.log");
    }

    [Fact]
    public void AddPapyrusMonitorCore_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddPapyrusMonitorCore();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddPapyrusMonitorCore_CanResolveAllDependenciesForMonitorService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging since it's required by services

        // Act
        services.AddPapyrusMonitorCore();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should be able to create the monitor service with all dependencies
        var action = () => serviceProvider.GetService<IPapyrusMonitorService>();
        action.Should().NotThrow();

        var monitorService = serviceProvider.GetService<IPapyrusMonitorService>();
        monitorService.Should().NotBeNull();
    }

    [Fact]
    public void AddPapyrusMonitorCore_MultipleCalls_CreatesMultipleRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPapyrusMonitorCore();
        services.AddPapyrusMonitorCore(); // Call twice

        // Assert - Currently allows duplicate registrations
        var serviceDescriptors = services.Where(s => s.ServiceType == typeof(IPapyrusMonitorService)).ToList();
        serviceDescriptors.Should().HaveCount(2);

        // Note: This is the current behavior. If you want to prevent duplicates,
        // the implementation would need to check if services are already registered
    }

    [Theory]
    [InlineData(typeof(IFileSystem), typeof(FileSystem))]
    [InlineData(typeof(ILogParser), typeof(PapyrusLogParser))]
    [InlineData(typeof(IFileWatcher), typeof(FileWatcher))]
    [InlineData(typeof(IFileTailReader), typeof(FileTailReader))]
    [InlineData(typeof(IPapyrusMonitorService), typeof(PapyrusMonitorService))]
    [InlineData(typeof(ISettingsService), typeof(JsonSettingsService))]
    [InlineData(typeof(IExportService), typeof(ExportService))]
    [InlineData(typeof(ISessionHistoryService), typeof(SessionHistoryService))]
    [InlineData(typeof(ITrendAnalysisService), typeof(TrendAnalysisService))]
    public void AddPapyrusMonitorCore_RegistersExpectedImplementations(Type serviceType, Type implementationType)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPapyrusMonitorCore();

        // Assert
        var descriptor = services.FirstOrDefault(s => s.ServiceType == serviceType);
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(implementationType);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
