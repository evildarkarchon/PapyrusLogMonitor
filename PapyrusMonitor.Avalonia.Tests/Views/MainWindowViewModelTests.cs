using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;
using System.Reactive.Subjects;
using ReactiveUI;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using System.Reactive;

namespace PapyrusMonitor.Avalonia.Tests.Views;

public class MainWindowViewModelTests : IDisposable
{
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ITrendAnalysisService> _mockTrendAnalysisService;
    private readonly Mock<IExportService> _mockExportService;
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly Mock<ISchedulerProvider> _mockSchedulerProvider;
    private readonly Mock<PapyrusMonitor.Core.Interfaces.ILogger> _mockLogger;
    private readonly Mock<IStorageProvider> _mockStorageProvider;
    private readonly ServiceProvider _serviceProvider;

    public MainWindowViewModelTests()
    {
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockTrendAnalysisService = new Mock<ITrendAnalysisService>();
        _mockExportService = new Mock<IExportService>();
        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _mockSchedulerProvider = new Mock<ISchedulerProvider>();
        _mockLogger = new Mock<PapyrusMonitor.Core.Interfaces.ILogger>();
        _mockStorageProvider = new Mock<IStorageProvider>();

        // Setup default returns
        _mockSettingsService.Setup(x => x.Settings).Returns(new AppSettings());
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(Observable.Never<AppSettings>());
        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(new Subject<PapyrusMonitor.Core.Models.PapyrusStats>());
        _mockMonitorService.Setup(x => x.Errors).Returns(new Subject<string>());
        _mockSchedulerProvider.Setup(x => x.MainThread).Returns(RxApp.MainThreadScheduler);
        _mockSchedulerProvider.Setup(x => x.TaskPool).Returns(RxApp.TaskpoolScheduler);
        
        // Setup TrendAnalysisService to return empty data
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusMonitor.Core.Models.PapyrusStats>>(), It.IsAny<int>()))
            .Returns(Task.FromResult(new TrendAnalysisResult()));
            
        // Setup SessionHistoryService
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics())
            .Returns(new List<PapyrusMonitor.Core.Models.PapyrusStats>());

        var services = new ServiceCollection();
        services.AddSingleton(_mockMonitorService.Object);
        services.AddSingleton(_mockSettingsService.Object);
        services.AddSingleton(_mockTrendAnalysisService.Object);
        services.AddSingleton(_mockExportService.Object);
        services.AddSingleton(_mockSessionHistoryService.Object);
        services.AddSingleton(_mockSchedulerProvider.Object);
        services.AddSingleton(_mockLogger.Object);
        services.AddSingleton(_mockStorageProvider.Object);
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<PapyrusMonitorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TrendAnalysisViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public void MainWindow_Should_Have_Correct_Default_Properties()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.Should().NotBeNull();
        viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
        // SettingsViewModel and TrendAnalysisViewModel are created on demand
        viewModel.SettingsViewModel.Should().BeNull();
        viewModel.TrendAnalysisViewModel.Should().BeNull();
    }

    [Fact]
    public void MainWindowViewModel_Should_Initialize_Correctly()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.ShowSettings.Should().BeFalse();
        viewModel.ShowTrendAnalysis.Should().BeFalse();
        viewModel.IsExporting.Should().BeFalse();
        
        viewModel.ShowSettingsCommand.Should().NotBeNull();
        viewModel.CloseSettingsCommand.Should().NotBeNull();
        viewModel.ShowTrendAnalysisCommand.Should().NotBeNull();
        viewModel.CloseTrendAnalysisCommand.Should().NotBeNull();
        viewModel.ExportCommand.Should().NotBeNull();
    }

    [Fact]
    public void ShowSettings_Command_Should_Toggle_Settings_Visibility()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.ShowSettings.Should().BeFalse();
        
        viewModel.ShowSettingsCommand.Execute().Subscribe();
        
        viewModel.ShowSettings.Should().BeTrue();
    }
    
    [Fact]
    public void CloseSettings_Command_Should_Hide_Settings()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.ShowSettingsCommand.Execute().Subscribe();
        viewModel.ShowSettings.Should().BeTrue();
        
        viewModel.CloseSettingsCommand.Execute().Subscribe();
        
        viewModel.ShowSettings.Should().BeFalse();
    }
    
    [Fact]
    public void ShowTrendAnalysis_Command_Should_Toggle_TrendAnalysis_Visibility()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.ShowTrendAnalysis.Should().BeFalse();
        
        viewModel.ShowTrendAnalysisCommand.Execute().Subscribe();
        
        viewModel.ShowTrendAnalysis.Should().BeTrue();
    }
    
    [Fact]
    public void CloseTrendAnalysis_Command_Should_Hide_TrendAnalysis()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        viewModel.ShowTrendAnalysisCommand.Execute().Subscribe();
        viewModel.ShowTrendAnalysis.Should().BeTrue();
        
        viewModel.CloseTrendAnalysisCommand.Execute().Subscribe();
        
        viewModel.ShowTrendAnalysis.Should().BeFalse();
    }
    
    [Fact]
    public void Settings_And_TrendAnalysis_Can_Be_Open_Simultaneously()
    {
        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
        
        // Open settings
        viewModel.ShowSettingsCommand.Execute().Subscribe();
        viewModel.ShowSettings.Should().BeTrue();
        
        // Open trend analysis - both can be open at the same time
        viewModel.ShowTrendAnalysisCommand.Execute().Subscribe();
        
        viewModel.ShowSettings.Should().BeTrue();
        viewModel.ShowTrendAnalysis.Should().BeTrue();
    }
}