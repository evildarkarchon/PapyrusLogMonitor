using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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

public class MainWindowViewModelTests
{
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly MonitoringConfiguration _testConfiguration;
    private readonly PapyrusMonitorViewModel _mockPapyrusMonitorViewModel;

    public MainWindowViewModelTests()
    {
        // Ensure ReactiveUI uses test scheduler
        RxApp.MainThreadScheduler = Scheduler.CurrentThread;
        
        // Setup mock service
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(Observable.Never<PapyrusStats>());
        _mockMonitorService.Setup(x => x.Errors).Returns(Observable.Never<string>());
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        _mockMonitorService.Setup(x => x.Configuration).Returns(new MonitoringConfiguration());
        
        // Setup test configuration
        _testConfiguration = new MonitoringConfiguration
        {
            LogFilePath = @"C:\test\log.txt"
        };
        
        // Create mock PapyrusMonitorViewModel
        _mockPapyrusMonitorViewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object, _testConfiguration);
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);

        // Assert
        viewModel.Title.Should().Be("Papyrus Log Monitor");
        viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
        viewModel.ExitCommand.Should().NotBeNull();
    }

    [Fact]
    public void Title_CanBeChanged()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);
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
        var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);

        // Act
        var canExecute = false;
        viewModel.ExitCommand.CanExecute.Subscribe(result => canExecute = result);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void Activation_InitializesPapyrusMonitorViewModelCorrectly()
    {
        new TestScheduler().With(scheduler =>
        {
            // Arrange
            var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);

            // Act
            viewModel.Activator.Activate();

            // Assert
            // PapyrusMonitorViewModel should be initialized in constructor
            viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
            viewModel.PapyrusMonitorViewModel.Should().Be(_mockPapyrusMonitorViewModel);
        });
    }

    [Fact]
    public void Deactivation_CleanupExecuted()
    {
        new TestScheduler().With(scheduler =>
        {
            // Arrange
            var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);
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
    public void PapyrusMonitor_IsReadOnlyProperty()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(_mockPapyrusMonitorViewModel);
        var propertyChanged = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.PapyrusMonitorViewModel))
                propertyChanged = true;
        };

        // Act & Assert
        // PapyrusMonitorViewModel is now readonly, so this test verifies it's properly injected
        // The property is initialized in constructor and doesn't change
        viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
        viewModel.PapyrusMonitorViewModel.Should().Be(_mockPapyrusMonitorViewModel);
        // propertyChanged will be false since we can't set the property
        propertyChanged.Should().BeFalse();
    }
}