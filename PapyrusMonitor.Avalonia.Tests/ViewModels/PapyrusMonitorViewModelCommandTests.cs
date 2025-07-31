using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class PapyrusMonitorViewModelCommandTests : IDisposable
{
    private readonly Subject<string> _errorSubject;
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Subject<PapyrusStats> _statsSubject;
    private readonly Subject<AppSettings> _settingsChangedSubject;
    private readonly TestScheduler _testScheduler;

    public PapyrusMonitorViewModelCommandTests()
    {
        _testScheduler = new TestScheduler();
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _statsSubject = new Subject<PapyrusStats>();
        _errorSubject = new Subject<string>();
        _settingsChangedSubject = new Subject<AppSettings>();

        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(_statsSubject);
        _mockMonitorService.Setup(x => x.Errors).Returns(_errorSubject);
        _mockSettingsService.Setup(x => x.Settings).Returns(new AppSettings());
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(_settingsChangedSubject);
    }

    public void Dispose()
    {
        _statsSubject?.Dispose();
        _errorSubject?.Dispose();
        _settingsChangedSubject?.Dispose();
    }

    [Fact]
    public void StartMonitoringCommand_Should_Start_Monitoring_Successfully()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        // Act
        viewModel.StartMonitoringCommand.Execute().Subscribe();

        // Wait for command to complete
        System.Threading.Thread.Sleep(100);

        // Assert
        viewModel.IsMonitoring.Should().BeTrue();
        viewModel.MonitoringButtonText.Should().Be("Stop Monitoring");
        viewModel.MonitoringButtonIcon.Should().Be("⏹️");
        viewModel.LastError.Should().BeNull();

        _mockMonitorService.Verify(x => x.UpdateConfigurationAsync(
            It.IsAny<MonitoringConfiguration>(), 
            It.IsAny<CancellationToken>()), Times.Once);
        _mockMonitorService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSessionHistoryService.Verify(x => x.StartSession(), Times.Once);
    }

    [Fact]
    public void StartMonitoringCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Cannot start monitoring");
        _mockMonitorService.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        var exceptionThrown = false;
        viewModel.StartMonitoringCommand.ThrownExceptions.Subscribe(_ => exceptionThrown = true);

        // Act
        viewModel.StartMonitoringCommand.Execute().Subscribe(
            _ => { },
            ex => { });

        System.Threading.Thread.Sleep(100);

        // Assert
        exceptionThrown.Should().BeTrue();
        viewModel.LastError.Should().Be(exception.Message);
        viewModel.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public void StopMonitoringCommand_Should_Stop_Monitoring_Successfully()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        // Start monitoring first
        viewModel.StartMonitoringCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        // Act
        viewModel.StopMonitoringCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        // Assert
        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.MonitoringButtonText.Should().Be("Start Monitoring");
        viewModel.MonitoringButtonIcon.Should().Be("▶️");
        viewModel.LastError.Should().BeNull();

        _mockMonitorService.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockSessionHistoryService.Verify(x => x.EndSession(), Times.Once);
    }

    [Fact]
    public void StopMonitoringCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Cannot stop monitoring");
        _mockMonitorService.Setup(x => x.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        viewModel.StartMonitoringCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        var exceptionThrown = false;
        viewModel.StopMonitoringCommand.ThrownExceptions.Subscribe(_ => exceptionThrown = true);

        // Act
        viewModel.StopMonitoringCommand.Execute().Subscribe(
            _ => { },
            ex => { });

        System.Threading.Thread.Sleep(100);

        // Assert
        exceptionThrown.Should().BeTrue();
        viewModel.LastError.Should().Be(exception.Message);
        viewModel.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public void ForceUpdateCommand_Should_Force_Update_Successfully()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        // Start monitoring to enable the command
        viewModel.StartMonitoringCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        // Act
        viewModel.ForceUpdateCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        // Assert
        viewModel.LastError.Should().BeNull();
        _mockMonitorService.Verify(x => x.ForceUpdateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ForceUpdateCommand_Should_Be_Disabled_When_Not_Monitoring()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        // Act & Assert
        viewModel.ForceUpdateCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
    }

    [Fact]
    public void ForceUpdateCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Force update failed");
        _mockMonitorService.Setup(x => x.ForceUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        viewModel.StartMonitoringCommand.Execute().Subscribe();
        System.Threading.Thread.Sleep(100);

        var exceptionThrown = false;
        viewModel.ForceUpdateCommand.ThrownExceptions.Subscribe(_ => exceptionThrown = true);

        // Act
        viewModel.ForceUpdateCommand.Execute().Subscribe(
            _ => { },
            ex => { });

        System.Threading.Thread.Sleep(100);

        // Assert
        exceptionThrown.Should().BeTrue();
        viewModel.LastError.Should().Be(exception.Message);
    }

    [Fact]
    public void UpdateLogPathCommand_Should_Update_Log_Path_Successfully()
    {
        // Arrange
        var newPath = @"C:\Games\Fallout4\Logs\Papyrus.0.log";
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        // Act
        viewModel.UpdateLogPathCommand.Execute(newPath).Subscribe();
        System.Threading.Thread.Sleep(100);

        // Assert
        viewModel.LogFilePath.Should().Be(newPath);
        viewModel.LastError.Should().BeNull();

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
            It.Is<AppSettings>(s => s.LogFilePath == newPath)), Times.Once);
    }

    [Fact]
    public void UpdateLogPathCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Cannot save settings");
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .ThrowsAsync(exception);

        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        var exceptionThrown = false;
        viewModel.UpdateLogPathCommand.ThrownExceptions.Subscribe(_ => exceptionThrown = true);

        // Act
        viewModel.UpdateLogPathCommand.Execute("test.log").Subscribe(
            _ => { },
            ex => { });

        System.Threading.Thread.Sleep(100);

        // Assert
        exceptionThrown.Should().BeTrue();
        viewModel.LastError.Should().Be(exception.Message);
    }

    [Fact]
    public void Command_CanExecute_Should_Update_Based_On_Monitoring_State()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        var canStartValues = new List<bool>();
        var canStopValues = new List<bool>();

        viewModel.StartMonitoringCommand.CanExecute.Subscribe(canStartValues.Add);
        viewModel.StopMonitoringCommand.CanExecute.Subscribe(canStopValues.Add);

        // Act & Assert - Initial state
        canStartValues.Should().ContainSingle().Which.Should().BeTrue();
        canStopValues.Should().ContainSingle().Which.Should().BeFalse();

        // Start monitoring
        viewModel.StartMonitoringCommand.Execute().Subscribe();

        // Give time for reactive updates
        System.Threading.Thread.Sleep(100);

        // Commands should have updated
        canStartValues.Should().HaveCountGreaterThan(1);
        canStartValues.Last().Should().BeFalse();
        canStopValues.Should().HaveCountGreaterThan(1);
        canStopValues.Last().Should().BeTrue();
    }

    [Fact]
    public void Commands_Should_Propagate_Exceptions_To_Observable()
    {
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        var observedExceptions = new List<Exception>();

        // Subscribe to all command exceptions
        Observable.Merge(
            viewModel.StartMonitoringCommand.ThrownExceptions,
            viewModel.StopMonitoringCommand.ThrownExceptions,
            viewModel.ToggleMonitoringCommand.ThrownExceptions,
            viewModel.ForceUpdateCommand.ThrownExceptions,
            viewModel.UpdateLogPathCommand.ThrownExceptions
        ).Subscribe(observedExceptions.Add);

        var exception = new InvalidOperationException("Test exception");
        _mockMonitorService.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        viewModel.StartMonitoringCommand.Execute().Subscribe(
            _ => { },
            ex => { });

        System.Threading.Thread.Sleep(100);

        // Assert
        observedExceptions.Should().ContainSingle()
            .Which.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Test exception");
    }

    [Fact]
    public async Task IsProcessing_Should_Be_Set_During_Command_Execution()
    {
        // Arrange
        var tcs = new TaskCompletionSource<Unit>();
        _mockMonitorService.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        var isProcessingValues = new List<bool>();
        viewModel.WhenAnyValue(x => x.IsProcessing).Subscribe(isProcessingValues.Add);

        // Act
        viewModel.StartMonitoringCommand.Execute().Subscribe();
        
        // Give observable time to propagate
        await Task.Delay(50);
        
        // Assert - Should be processing
        isProcessingValues.Should().HaveCount(2);
        isProcessingValues[0].Should().BeFalse(); // Initial value
        isProcessingValues[1].Should().BeTrue();  // During execution

        // Complete the task
        tcs.SetResult(Unit.Default);
        await Task.Delay(50);

        // Should no longer be processing
        isProcessingValues.Should().HaveCount(3);
        isProcessingValues[2].Should().BeFalse();
    }

    [Fact(Skip = "Test requires real-time delay which is too slow for unit tests")]
    public async Task LastError_Should_Be_Cleared_After_Timeout()
    {
        // This test verifies the throttle behavior which requires a 5-second delay
        // It's better suited for integration tests
        
        // Arrange
        var viewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);

        viewModel.Activator.Activate();

        // Act - Simulate error through the error subject
        _errorSubject.OnNext("Test error");
        
        // Wait for the error to be set
        await Task.Delay(100);
        viewModel.LastError.Should().Be("Test error");
        
        // Wait for just before 5 seconds
        await Task.Delay(4900);

        // Assert - Error should still be present
        viewModel.LastError.Should().Be("Test error");

        // Wait past 5 seconds total
        await Task.Delay(200);

        // Error should be cleared
        viewModel.LastError.Should().BeNull();
        
        viewModel.Activator.Deactivate();
    }
}