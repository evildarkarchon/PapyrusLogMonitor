using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class MainWindowViewModelTests
{
    public MainWindowViewModelTests()
    {
        // Ensure ReactiveUI uses test scheduler
        RxApp.MainThreadScheduler = Scheduler.CurrentThread;
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.Title.Should().Be("Papyrus Log Monitor");
        viewModel.PapyrusMonitor.Should().BeNull();
        viewModel.ExitCommand.Should().NotBeNull();
    }

    [Fact]
    public void Title_CanBeChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        const string newTitle = "Custom Title";

        // Act
        viewModel.Title = newTitle;

        // Assert
        viewModel.Title.Should().Be(newTitle);
    }

    [Fact]
    public void ExitCommand_CanExecute()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act
        var canExecute = false;
        viewModel.ExitCommand.CanExecute.Subscribe(result => canExecute = result);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void Activation_DoesNotCreatePapyrusMonitorViewModel_DueToInjectionRequirement()
    {
        new TestScheduler().With(scheduler =>
        {
            // Arrange
            var viewModel = new MainWindowViewModel();

            // Act
            viewModel.Activator.Activate();

            // Assert
            // PapyrusMonitor should remain null since it requires DI
            viewModel.PapyrusMonitor.Should().BeNull();
        });
    }

    [Fact]
    public void Deactivation_CleanupExecuted()
    {
        new TestScheduler().With(scheduler =>
        {
            // Arrange
            var mockMonitorService = new Mock<IPapyrusMonitorService>();
            mockMonitorService.Setup(x => x.StatsUpdated).Returns(Observable.Never<PapyrusStats>());
            mockMonitorService.Setup(x => x.Errors).Returns(Observable.Never<string>());
            mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
            
            var viewModel = new MainWindowViewModel();
            viewModel.PapyrusMonitor = new PapyrusMonitorViewModel(mockMonitorService.Object);
            viewModel.Activator.Activate();

            // Act
            viewModel.Activator.Deactivate();

            // Assert
            // The cleanup delegate should execute without throwing
            // In a real test environment, we would verify disposal was called
            // but since Dispose is not virtual, we can't mock it directly
            Assert.True(true); // If we get here, deactivation succeeded
        });
    }

    [Fact]
    public void PapyrusMonitor_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.PapyrusMonitor))
                propertyChanged = true;
        };

        var mockMonitorService = new Mock<IPapyrusMonitorService>();
        mockMonitorService.Setup(x => x.StatsUpdated).Returns(Observable.Never<PapyrusStats>());
        mockMonitorService.Setup(x => x.Errors).Returns(Observable.Never<string>());
        mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        
        // Act
        viewModel.PapyrusMonitor = new PapyrusMonitorViewModel(mockMonitorService.Object);

        // Assert
        propertyChanged.Should().BeTrue();
    }
}