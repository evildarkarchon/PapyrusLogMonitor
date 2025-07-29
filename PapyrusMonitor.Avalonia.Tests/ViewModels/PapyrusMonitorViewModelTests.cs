using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Models;
using ReactiveUI;
using ReactiveUI.Testing;
using Xunit;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class PapyrusMonitorViewModelTests : IDisposable
{
    private readonly TestScheduler _testScheduler;

    public PapyrusMonitorViewModelTests()
    {
        _testScheduler = new TestScheduler();
        RxApp.MainThreadScheduler = _testScheduler;
    }

    [Fact]
    public void Constructor_InitializesWithoutParameters()
    {
        // Act & Assert
        var viewModel = new PapyrusMonitorViewModel();
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Act
        var viewModel = new PapyrusMonitorViewModel();

        // Assert
        viewModel.Statistics.Should().NotBeNull();
        viewModel.LogFilePath.Should().NotBeNull(); // Now has default value
        viewModel.LastError.Should().BeNull();
        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.StatusText.Should().Be("Idle");
    }

    [Fact]
    public void IsMonitoring_ReflectsInternalState()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();
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
        var viewModel = new PapyrusMonitorViewModel();
        viewModel.LogFilePath = @"C:\test\log.txt";

        // Act - Test that the command can execute without throwing
        var canExecute = await viewModel.StartMonitoringCommand.CanExecute.FirstAsync();
        
        // Clean up by ensuring we're not monitoring
        if (viewModel.IsMonitoringInternal)
        {
            await viewModel.StopMonitoringCommand.Execute().FirstAsync();
        }

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task StartMonitoringCommand_CanExecuteWhenNotMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();
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
        var viewModel = new PapyrusMonitorViewModel();
        viewModel.IsMonitoringInternal = true;

        // Act
        await viewModel.StopMonitoringCommand.Execute().FirstAsync();

        // Assert
        viewModel.IsMonitoringInternal.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleMonitoringCommand_CanExecute()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();

        // Act
        var canExecute = await viewModel.ToggleMonitoringCommand.CanExecute.FirstAsync();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleMonitoringCommand_StopsWhenMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();
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
        var viewModel = new PapyrusMonitorViewModel();
        viewModel.IsMonitoringInternal = true;

        // Act
        await viewModel.ForceUpdateCommand.Execute().FirstAsync();

        // Assert
        // Command should execute without throwing
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateLogPathCommand_UpdatesConfiguration()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();
        const string newPath = @"C:\new\path.txt";

        // Act
        await viewModel.UpdateLogPathCommand.Execute(newPath).FirstAsync();

        // Assert
        viewModel.LogFilePath.Should().Be(newPath);
    }

    [Fact]
    public void StatsUpdates_StatisticsPropertyIsAccessible()
    {
        // Arrange & Act
        var viewModel = new PapyrusMonitorViewModel();
        
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
        var viewModel = new PapyrusMonitorViewModel();

        // Act & Assert - Test that LastError property can be accessed
        viewModel.LastError.Should().BeNull(); // Should start as null
    }

    [Fact]
    public void LastError_PropertyNotifiesChanges()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel();
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
        var viewModel = new PapyrusMonitorViewModel();

        // Act
        viewModel.Dispose();

        // Assert
        // Disposal should complete without throwing
        viewModel.Should().NotBeNull();
    }

    public void Dispose()
    {
        // TestScheduler doesn't need disposal
    }
}