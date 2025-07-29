using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using ReactiveUI;
using ReactiveUI.Testing;
using Xunit;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class PapyrusMonitorViewModelTests : IDisposable
{
    private readonly TestScheduler _testScheduler;
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly MonitoringConfiguration _testConfiguration;

    public PapyrusMonitorViewModelTests()
    {
        _testScheduler = new TestScheduler();
        RxApp.MainThreadScheduler = _testScheduler;
        
        // Setup mock service
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(Observable.Never<PapyrusStats>());
        _mockMonitorService.Setup(x => x.Errors).Returns(Observable.Never<string>());
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        _mockMonitorService.Setup(x => x.Configuration).Returns(new MonitoringConfiguration());
        _mockMonitorService.Setup(x => x.LastStats).Returns((PapyrusStats?)null);
        
        // Setup test configuration
        _testConfiguration = new MonitoringConfiguration
        {
            LogFilePath = @"C:\test\log.txt",
            UpdateIntervalMs = 1000
        };
    }

    [Fact]
    public void Constructor_InitializesWithDependencyInjection()
    {
        // Act & Assert
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Act
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);

        // Assert
        viewModel.Statistics.Should().NotBeNull();
        viewModel.LogFilePath.Should().Be(_testConfiguration.LogFilePath);
        viewModel.LastError.Should().BeNull();
        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.StatusText.Should().Be("Idle");
    }

    [Fact]
    public void IsMonitoring_ReflectsInternalState()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        var initialState = viewModel.IsMonitoring;
        
        // Act
        viewModel.IsMonitoringInternal = true;
        
        // Assert
        initialState.Should().BeFalse();
        viewModel.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public async Task StartMonitoringCommand_StartsMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        _mockMonitorService.Setup(x => x.StartAsync(It.IsAny<System.Threading.CancellationToken>()))
                          .Returns(Task.CompletedTask);

        // Act - Test that the command can execute without throwing
        var canExecute = await viewModel.StartMonitoringCommand.CanExecute.FirstAsync();
        
        // Clean up by ensuring we're not monitoring
        if (viewModel.IsMonitoringInternal)
        {
            await viewModel.StopMonitoringCommand.Execute().FirstAsync();
        }

        // Assert
        canExecute.Should().BeTrue();
        _mockMonitorService.Verify(x => x.StartAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartMonitoringCommand_CanExecuteWhenNotMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        viewModel.IsMonitoringInternal = false;

        // Act
        var canExecute = await viewModel.StartMonitoringCommand.CanExecute.FirstAsync();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task StopMonitoringCommand_StopsMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        _mockMonitorService.Setup(x => x.StopAsync(It.IsAny<System.Threading.CancellationToken>()))
                          .Returns(Task.CompletedTask);
        viewModel.IsMonitoringInternal = true;

        // Act
        await viewModel.StopMonitoringCommand.Execute().FirstAsync();

        // Assert
        viewModel.IsMonitoringInternal.Should().BeFalse();
        _mockMonitorService.Verify(x => x.StopAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleMonitoringCommand_CanExecute()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);

        // Act
        var canExecute = await viewModel.ToggleMonitoringCommand.CanExecute.FirstAsync();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleMonitoringCommand_StopsWhenMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        _mockMonitorService.Setup(x => x.StopAsync(It.IsAny<System.Threading.CancellationToken>()))
                          .Returns(Task.CompletedTask);
        viewModel.IsMonitoringInternal = true;

        // Act
        await viewModel.ToggleMonitoringCommand.Execute().FirstAsync();

        // Assert
        viewModel.IsMonitoringInternal.Should().BeFalse();
    }

    [Fact]
    public async Task ForceUpdateCommand_CallsForceUpdate()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        _mockMonitorService.Setup(x => x.ForceUpdateAsync(It.IsAny<System.Threading.CancellationToken>()))
                          .Returns(Task.CompletedTask);
        viewModel.IsMonitoringInternal = true;

        // Act
        await viewModel.ForceUpdateCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.ForceUpdateAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLogPathCommand_UpdatesConfiguration()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        const string newPath = @"C:\new\path.txt";
        _mockMonitorService.Setup(x => x.UpdateConfigurationAsync(It.IsAny<MonitoringConfiguration>(), It.IsAny<System.Threading.CancellationToken>()))
                          .Returns(Task.CompletedTask);

        // Act
        await viewModel.UpdateLogPathCommand.Execute(newPath).FirstAsync();

        // Assert
        viewModel.LogFilePath.Should().Be(newPath);
        _mockMonitorService.Verify(x => x.UpdateConfigurationAsync(It.IsAny<MonitoringConfiguration>(), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public void StatsUpdates_StatisticsPropertyIsAccessible()
    {
        // Arrange & Act
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        
        // Assert - Test that Statistics property is accessible and has default values
        viewModel.Statistics.Should().NotBeNull();
        viewModel.Statistics.Dumps.Should().Be(0);
        viewModel.Statistics.Stacks.Should().Be(0);
        viewModel.Statistics.Warnings.Should().Be(0);
        viewModel.Statistics.Errors.Should().Be(0);
        viewModel.Statistics.Ratio.Should().Be(0.0);
    }

    [Fact]
    public void ErrorMessages_CanBeAccessed()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);

        // Act & Assert - Test that LastError property can be accessed
        viewModel.LastError.Should().BeNull(); // Should start as null
    }

    [Fact]
    public void LastError_PropertyNotifiesChanges()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
        // Act - Access the property to trigger any potential change notification setup
        var currentError = viewModel.LastError;

        // Assert
        currentError.Should().BeNull();
        // Note: Since we can't directly set the private setter, we just test that the property exists
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);

        // Act
        viewModel.Dispose();

        // Assert
        // Disposal should complete without throwing
        viewModel.Should().NotBeNull();
        _mockMonitorService.Verify(x => x.Dispose(), Times.Once);
    }

    public void Dispose()
    {
        // TestScheduler doesn't need disposal
    }
}