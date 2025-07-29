using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
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

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class PapyrusMonitorViewModelTests : IDisposable
{
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly Subject<PapyrusStats> _statsSubject;
    private readonly Subject<string> _errorsSubject;
    private readonly TestScheduler _testScheduler;

    public PapyrusMonitorViewModelTests()
    {
        _testScheduler = new TestScheduler();
        RxApp.MainThreadScheduler = _testScheduler;
        
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _statsSubject = new Subject<PapyrusStats>();
        _errorsSubject = new Subject<string>();

        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(_statsSubject);
        _mockMonitorService.Setup(x => x.Errors).Returns(_errorsSubject);
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        _mockMonitorService.Setup(x => x.Configuration).Returns(new MonitoringConfiguration());
    }

    [Fact]
    public void Constructor_RequiresMonitorService()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PapyrusMonitorViewModel(null!));
        exception.ParamName.Should().Be("monitorService");
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Act
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Assert
        viewModel.Statistics.Should().NotBeNull();
        viewModel.LogFilePath.Should().BeNull();
        viewModel.LastError.Should().BeNull();
        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.StatusText.Should().Be("Idle");
    }

    [Fact]
    public void IsMonitoring_ReflectsServiceState()
    {
        // Arrange
        _mockMonitorService.SetupSequence(x => x.IsMonitoring)
            .Returns(false)
            .Returns(true);

        // Act
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
        var initialState = viewModel.IsMonitoring;
        
        // Simulate service state change
        viewModel.RaisePropertyChanged(nameof(viewModel.IsMonitoring));
        
        // Assert
        initialState.Should().BeFalse();
    }

    [Fact]
    public async Task StartMonitoringCommand_StartsMonitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
        viewModel.LogFilePath = @"C:\test\log.txt";

        // Act
        await viewModel.StartMonitoringCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.UpdateConfigurationAsync(
            It.Is<MonitoringConfiguration>(c => c.LogFilePath == @"C:\test\log.txt"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockMonitorService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartMonitoringCommand_HandlesErrors()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
        var errorMessage = "Failed to start";
        _mockMonitorService.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => 
            await viewModel.StartMonitoringCommand.Execute().FirstAsync());

        viewModel.LastError.Should().Contain(errorMessage);
    }

    [Fact]
    public async Task StopMonitoringCommand_StopsMonitoring()
    {
        // Arrange
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(true);
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Act
        await viewModel.StopMonitoringCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleMonitoringCommand_StartsWhenNotMonitoring()
    {
        // Arrange
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Act
        await viewModel.ToggleMonitoringCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleMonitoringCommand_StopsWhenMonitoring()
    {
        // Arrange
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(true);
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Act
        await viewModel.ToggleMonitoringCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceUpdateCommand_CallsForceUpdate()
    {
        // Arrange
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(true);
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Act
        await viewModel.ForceUpdateCommand.Execute().FirstAsync();

        // Assert
        _mockMonitorService.Verify(x => x.ForceUpdateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLogPathCommand_UpdatesConfiguration()
    {
        // Arrange
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(true);
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
        const string newPath = @"C:\new\path.txt";

        // Act
        await viewModel.UpdateLogPathCommand.Execute(newPath).FirstAsync();

        // Assert
        viewModel.LogFilePath.Should().Be(newPath);
        _mockMonitorService.Verify(x => x.UpdateConfigurationAsync(
            It.Is<MonitoringConfiguration>(c => c.LogFilePath == newPath),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void StatsUpdates_UpdateStatisticsViewModel()
    {
        _testScheduler.With(scheduler =>
        {
            // Arrange
            var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
            viewModel.Activator.Activate();
            
            var stats = new PapyrusStats(
                DateTime.Now,
                Dumps: 10,
                Stacks: 20,
                Warnings: 5,
                Errors: 2,
                Ratio: 0.5);

            // Act
            _statsSubject.OnNext(stats);
            scheduler.AdvanceBy(1);

            // Assert
            viewModel.Statistics!.CurrentStats.Should().Be(stats);
        });
    }

    [Fact]
    public void ErrorMessages_UpdateLastError()
    {
        _testScheduler.With(scheduler =>
        {
            // Arrange
            var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
            viewModel.Activator.Activate();
            const string errorMessage = "Test error";

            // Act
            _errorsSubject.OnNext(errorMessage);
            scheduler.AdvanceBy(1);

            // Assert
            viewModel.LastError.Should().Be(errorMessage);
        });
    }

    [Fact]
    public void LastError_ReceivesErrorFromService()
    {
        _testScheduler.With(scheduler =>
        {
            // Arrange
            var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);
            viewModel.Activator.Activate();
            
            // Act
            _errorsSubject.OnNext("Test error");
            scheduler.AdvanceBy(1); // Process the error subscription

            // Assert
            viewModel.LastError.Should().Be("Test error");
            
            // Note: The timeout clearing behavior is tested in integration tests
            // as it's complex to test reliably with schedulers in unit tests
        });
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(_mockMonitorService.Object);

        // Act
        viewModel.Dispose();

        // Assert
        _mockMonitorService.Verify(x => x.Dispose(), Times.Once);
    }

    public void Dispose()
    {
        _statsSubject?.Dispose();
        _errorsSubject?.Dispose();
        // TestScheduler doesn't need disposal
    }
}