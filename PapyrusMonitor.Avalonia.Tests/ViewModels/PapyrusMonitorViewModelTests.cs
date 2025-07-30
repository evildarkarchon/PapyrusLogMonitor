using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using System.Reactive.Subjects;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class PapyrusMonitorViewModelTests2 : IDisposable
{
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly ServiceProvider _serviceProvider;
    private readonly Subject<PapyrusStats> _statsSubject;
    private readonly Subject<string> _errorSubject;

    public PapyrusMonitorViewModelTests2()
    {
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _statsSubject = new Subject<PapyrusStats>();
        _errorSubject = new Subject<string>();

        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(_statsSubject);
        _mockMonitorService.Setup(x => x.Errors).Returns(_errorSubject);
        _mockSettingsService.Setup(x => x.Settings).Returns(new AppSettings());
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(Observable.Never<AppSettings>());

        var services = new ServiceCollection();
        services.AddSingleton(_mockMonitorService.Object);
        services.AddSingleton(_mockSettingsService.Object);
        services.AddSingleton(_mockSessionHistoryService.Object);
        services.AddTransient<PapyrusMonitorViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _statsSubject?.Dispose();
        _errorSubject?.Dispose();
    }

    private void ActivateViewModel(PapyrusMonitorViewModel viewModel)
    {
        // Simulate view activation to trigger HandleActivation
        var disposables = new CompositeDisposable();
        viewModel.Activator.Activate();
    }

    [Fact]
    public void Should_Initialize_With_Default_Values()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();

        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.IsProcessing.Should().BeFalse();
        viewModel.EnableAnimations.Should().BeTrue();
        viewModel.MonitoringButtonText.Should().Be("Start Monitoring");
        viewModel.MonitoringButtonIcon.Should().Be("▶️");
    }

    [Fact]
    public void Should_Initialize_Statistics_With_Zero_Values()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();

        viewModel.Statistics.Dumps.Should().Be(0);
        viewModel.Statistics.Stacks.Should().Be(0);
        viewModel.Statistics.Warnings.Should().Be(0);
        viewModel.Statistics.Errors.Should().Be(0);
        viewModel.Statistics.Ratio.Should().Be(0);
    }

    [Fact]
    public void Should_Update_Statistics_When_Stats_Updated()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);
        
        var newStats = new PapyrusStats(
            Timestamp: DateTime.Now,
            Dumps: 10,
            Stacks: 20,
            Warnings: 5,
            Errors: 2,
            Ratio: 0.5
        );

        _statsSubject.OnNext(newStats);

        viewModel.Statistics.Should().Be(newStats);
        viewModel.LastUpdateTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Update_Status_Indicators_Based_On_Values()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);

        // Test with zero values - should show all green
        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 0, 0, 0.3));
        
        viewModel.RatioStatus.Should().Be("✓");
        viewModel.RatioStatusColor.ToString().Should().Contain("Green");
        viewModel.WarningsStatus.Should().Be("✓");
        viewModel.WarningsStatusColor.ToString().Should().Contain("Green");
        viewModel.ErrorsStatus.Should().Be("✓");
        viewModel.ErrorsStatusColor.ToString().Should().Contain("Green");

        // Any warnings > 0 should show warning
        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 1, 0, 0.3));
        
        viewModel.WarningsStatus.Should().Be("⚠️");
        viewModel.WarningsStatusColor.ToString().Should().Contain("Orange");

        // Any errors > 0 should show error
        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 0, 1, 0.3));
        
        viewModel.ErrorsStatus.Should().Be("❌");
        viewModel.ErrorsStatusColor.ToString().Should().Contain("Red");
    }

    [Fact]
    public void ToggleMonitoringCommand_Should_Start_Monitoring()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();

        viewModel.ToggleMonitoringCommand.Execute().Subscribe();

        viewModel.IsMonitoring.Should().BeTrue();
        viewModel.MonitoringButtonText.Should().Be("Stop Monitoring");
        viewModel.MonitoringButtonIcon.Should().Be("⏹️");
        
        _mockMonitorService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ToggleMonitoringCommand_Should_Stop_Monitoring_When_Running()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        
        // Start monitoring first
        viewModel.ToggleMonitoringCommand.Execute().Subscribe();
        viewModel.IsMonitoring.Should().BeTrue();

        // Stop monitoring
        viewModel.ToggleMonitoringCommand.Execute().Subscribe();

        viewModel.IsMonitoring.Should().BeFalse();
        viewModel.MonitoringButtonText.Should().Be("Start Monitoring");
        viewModel.MonitoringButtonIcon.Should().Be("▶️");
        
        _mockMonitorService.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Should_Display_Status_Message_For_High_Warnings()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);

        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 50, 2, 0.5));

        viewModel.HasStatusMessage.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("2 errors detected in Papyrus log!");
        // The color is in hex format
        viewModel.StatusMessageBackground.ToString().Should().Contain("#ffffe6e6"); // Light red because errors > 0
    }

    [Fact]
    public void Should_Display_Status_Message_For_High_Errors()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);

        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 50, 100, 0.5));

        viewModel.HasStatusMessage.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("100 errors detected in Papyrus log!");
        // The color is in hex format
        viewModel.StatusMessageBackground.ToString().Should().Contain("#ffffe6e6"); // Light red
    }

    [Fact]
    public void Should_Display_Normal_Status_Message_For_Low_Values()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);

        // Normal status - no errors, warnings, and ratio <= 0.5 means HasStatusMessage is false
        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 0, 0, 0.3));

        viewModel.HasStatusMessage.Should().BeFalse();
    }

    [Fact]
    public void Should_Display_Caution_Message_For_Elevated_Ratio()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);

        // Ratio between 0.5 and 0.8 should show caution message
        _statsSubject.OnNext(new PapyrusStats(DateTime.Now, 10, 20, 0, 0, 0.6));
        
        viewModel.HasStatusMessage.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Caution: Elevated dumps-to-stacks ratio.");
        viewModel.StatusMessageBackground.ToString().Should().Contain("#fffff4e5"); // Light orange (actual color)
    }

    [Fact]
    public void Should_Update_Last_Error_When_Error_Occurs()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);
        
        _errorSubject.OnNext("Test error message");

        viewModel.LastError.Should().Be("Test error message");
        viewModel.HasStatusMessage.Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Error: Test error message");
    }

    [Fact]
    public void Should_Call_SessionHistoryService_When_Starting_Monitoring()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();

        viewModel.ToggleMonitoringCommand.Execute().Subscribe();

        _mockSessionHistoryService.Verify(x => x.StartSession(), Times.Once);
    }

    [Fact]
    public void Should_Call_SessionHistoryService_When_Stopping_Monitoring()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        
        // Start and stop monitoring
        viewModel.ToggleMonitoringCommand.Execute().Subscribe();
        viewModel.ToggleMonitoringCommand.Execute().Subscribe();

        _mockSessionHistoryService.Verify(x => x.EndSession(), Times.Once);
    }

    [Fact]
    public void Should_Add_Stats_To_SessionHistory_When_Updated()
    {
        var viewModel = _serviceProvider.GetRequiredService<PapyrusMonitorViewModel>();
        ActivateViewModel(viewModel);
        
        var stats = new PapyrusStats(DateTime.Now, 10, 20, 5, 2, 0.5);

        _statsSubject.OnNext(stats);

        _mockSessionHistoryService.Verify(x => x.RecordStats(stats), Times.Once);
    }
}