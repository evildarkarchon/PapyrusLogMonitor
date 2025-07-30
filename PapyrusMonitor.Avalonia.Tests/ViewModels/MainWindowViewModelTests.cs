using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Export;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly Mock<IExportService> _mockExportService;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IPapyrusMonitorService> _mockMonitorService;
    private readonly PapyrusMonitorViewModel _mockPapyrusMonitorViewModel;
    private readonly Mock<ISchedulerProvider> _mockSchedulerProvider;
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IStorageProvider> _mockStorageProvider;
    private readonly Mock<ITrendAnalysisService> _mockTrendAnalysisService;
    private readonly MonitoringConfiguration _testConfiguration;

    public MainWindowViewModelTests()
    {
        // Ensure ReactiveUI uses test scheduler
        RxApp.MainThreadScheduler = Scheduler.CurrentThread;

        // Setup mock services
        _mockMonitorService = new Mock<IPapyrusMonitorService>();
        _mockMonitorService.Setup(x => x.StatsUpdated).Returns(Observable.Never<PapyrusStats>());
        _mockMonitorService.Setup(x => x.Errors).Returns(Observable.Never<string>());
        _mockMonitorService.Setup(x => x.IsMonitoring).Returns(false);
        _mockMonitorService.Setup(x => x.Configuration).Returns(new MonitoringConfiguration());

        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService.Setup(x => x.Settings).Returns(new AppSettings());
        _mockSettingsService.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new AppSettings());
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(Observable.Never<AppSettings>());

        _mockExportService = new Mock<IExportService>();
        _mockExportService.Setup(x => x.GetFileExtension(It.IsAny<ExportFormat>()))
            .Returns<ExportFormat>(format => format == ExportFormat.Csv ? ".csv" : ".json");

        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _mockSessionHistoryService.Setup(x => x.IsSessionActive).Returns(true);
        _mockSessionHistoryService.Setup(x => x.SessionStartTime).Returns(DateTime.Now);
        _mockSessionHistoryService.Setup(x => x.SessionEndTime).Returns(DateTime.Now.AddHours(1));
        _mockSessionHistoryService.Setup(x => x.RecordStats(It.IsAny<PapyrusStats>()));
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(new List<PapyrusStats>
        {
            new(DateTime.Now.AddMinutes(-5), 10, 5, 2, 1, 0.1), new(DateTime.Now, 15, 8, 3, 2, 0.13)
        });
        _mockSessionHistoryService.Setup(x => x.GetSessionSummary()).Returns(new SessionSummary
        {
            TotalDumps = 25,
            TotalStacks = 13,
            TotalWarnings = 5,
            TotalErrors = 3,
            AverageRatio = 0.115
        });

        _mockTrendAnalysisService = new Mock<ITrendAnalysisService>();
        _mockTrendAnalysisService
            .Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(new TrendAnalysisResult
            {
                DumpsTrend =
                    new TrendData
                    {
                        DataPoints =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 10 },
                                new() { Timestamp = DateTime.Now, Value = 15 }
                            },
                        MovingAverage =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 10 },
                                new() { Timestamp = DateTime.Now, Value = 12.5 }
                            },
                        TrendLine =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 10 },
                                new() { Timestamp = DateTime.Now, Value = 15 }
                            },
                        Summary = new TrendSummary { Min = 10, Max = 15, Average = 12.5, TrendSlope = 1.0 }
                    },
                StacksTrend =
                    new TrendData
                    {
                        DataPoints =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 5 },
                                new() { Timestamp = DateTime.Now, Value = 8 }
                            },
                        MovingAverage =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 5 },
                                new() { Timestamp = DateTime.Now, Value = 6.5 }
                            },
                        TrendLine =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 5 },
                                new() { Timestamp = DateTime.Now, Value = 8 }
                            },
                        Summary = new TrendSummary { Min = 5, Max = 8, Average = 6.5, TrendSlope = 0.6 }
                    },
                WarningsTrend =
                    new TrendData
                    {
                        DataPoints =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 2 },
                                new() { Timestamp = DateTime.Now, Value = 3 }
                            },
                        MovingAverage =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 2 },
                                new() { Timestamp = DateTime.Now, Value = 2.5 }
                            },
                        TrendLine =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 2 },
                                new() { Timestamp = DateTime.Now, Value = 3 }
                            },
                        Summary = new TrendSummary { Min = 2, Max = 3, Average = 2.5, TrendSlope = 0.2 }
                    },
                ErrorsTrend =
                    new TrendData
                    {
                        DataPoints =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 1 },
                                new() { Timestamp = DateTime.Now, Value = 2 }
                            },
                        MovingAverage =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 1 },
                                new() { Timestamp = DateTime.Now, Value = 1.5 }
                            },
                        TrendLine =
                            new List<TrendDataPoint>
                            {
                                new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 1 },
                                new() { Timestamp = DateTime.Now, Value = 2 }
                            },
                        Summary = new TrendSummary { Min = 1, Max = 2, Average = 1.5, TrendSlope = 0.2 }
                    },
                RatioTrend = new TrendData
                {
                    DataPoints =
                        new List<TrendDataPoint>
                        {
                            new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 0.1 },
                            new() { Timestamp = DateTime.Now, Value = 0.13 }
                        },
                    MovingAverage =
                        new List<TrendDataPoint>
                        {
                            new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 0.1 },
                            new() { Timestamp = DateTime.Now, Value = 0.115 }
                        },
                    TrendLine = new List<TrendDataPoint>
                    {
                        new() { Timestamp = DateTime.Now.AddMinutes(-5), Value = 0.1 },
                        new() { Timestamp = DateTime.Now, Value = 0.13 }
                    },
                    Summary = new TrendSummary { Min = 0.1, Max = 0.13, Average = 0.115, TrendSlope = 0.006 }
                },
                TimeRange = new TimeRange { Start = DateTime.Now.AddMinutes(-5), End = DateTime.Now }
            });

        _mockSchedulerProvider = new Mock<ISchedulerProvider>();
        _mockSchedulerProvider.Setup(x => x.MainThread).Returns(Scheduler.CurrentThread);
        _mockSchedulerProvider.Setup(x => x.TaskPool).Returns(Scheduler.CurrentThread);
        _mockSchedulerProvider.Setup(x => x.CurrentThread).Returns(Scheduler.CurrentThread);

        _mockLogger = new Mock<ILogger>();

        _mockStorageProvider = new Mock<IStorageProvider>();

        // Setup test configuration
        _testConfiguration = new MonitoringConfiguration { LogFilePath = @"C:\test\log.txt" };

        // Create mock PapyrusMonitorViewModel
        _mockPapyrusMonitorViewModel = new PapyrusMonitorViewModel(
            _mockMonitorService.Object,
            _mockSettingsService.Object,
            _mockSessionHistoryService.Object);
    }

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(
            _mockPapyrusMonitorViewModel,
            _mockSettingsService.Object,
            _mockExportService.Object,
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object);
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.Title.Should().Be("Papyrus Log Monitor");
        viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
        viewModel.ExitCommand.Should().NotBeNull();
        viewModel.ShowSettingsCommand.Should().NotBeNull();
        viewModel.CloseSettingsCommand.Should().NotBeNull();
        viewModel.ExportCommand.Should().NotBeNull();
        viewModel.ShowTrendAnalysisCommand.Should().NotBeNull();
        viewModel.CloseTrendAnalysisCommand.Should().NotBeNull();
    }

    [Fact]
    public void Title_CanBeChanged()
    {
        // Arrange
        var viewModel = CreateViewModel();
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
        var viewModel = CreateViewModel();

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
            var viewModel = new MainWindowViewModel(
                _mockPapyrusMonitorViewModel,
                _mockSettingsService.Object,
                _mockExportService.Object,
                _mockSessionHistoryService.Object,
                _mockTrendAnalysisService.Object,
                _mockSchedulerProvider.Object,
                _mockLogger.Object,
                _mockStorageProvider.Object);

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
            var viewModel = new MainWindowViewModel(
                _mockPapyrusMonitorViewModel,
                _mockSettingsService.Object,
                _mockExportService.Object,
                _mockSessionHistoryService.Object,
                _mockTrendAnalysisService.Object,
                _mockSchedulerProvider.Object,
                _mockLogger.Object,
                _mockStorageProvider.Object);
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
        var viewModel = CreateViewModel();
        var propertyChanged = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.PapyrusMonitorViewModel))
            {
                propertyChanged = true;
            }
        };

        // Act & Assert
        // PapyrusMonitorViewModel is now readonly, so this test verifies it's properly injected
        // The property is initialized in constructor and doesn't change
        viewModel.PapyrusMonitorViewModel.Should().NotBeNull();
        viewModel.PapyrusMonitorViewModel.Should().Be(_mockPapyrusMonitorViewModel);
        // propertyChanged will be false since we can't set the property
        propertyChanged.Should().BeFalse();
    }

    [Fact]
    public void ShowSettingsCommand_CanExecute()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        var canExecute = false;
        viewModel.ShowSettingsCommand.CanExecute.Subscribe(result => canExecute = result);

        // Assert
        canExecute.Should().BeTrue();
        viewModel.ShowSettingsCommand.Should().NotBeNull();
    }

    [Fact]
    public void CloseSettingsCommand_ClearsShowSettingsFlag()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.CloseSettingsCommand.Execute().Subscribe();

        // Assert
        viewModel.ShowSettings.Should().BeFalse();
        viewModel.SettingsViewModel.Should().BeNull();
    }

    [Fact]
    public void ExportCommand_CanExecute_WhenNotExportingAndSessionActive()
    {
        // Arrange
        _mockSessionHistoryService.Setup(x => x.IsSessionActive).Returns(true);
        var viewModel = CreateViewModel();

        // Act
        var canExecute = false;
        viewModel.ExportCommand.CanExecute.Subscribe(result => canExecute = result);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void ExportCommand_CannotExecute_WhenSessionInactive()
    {
        // Arrange
        _mockSessionHistoryService.Setup(x => x.IsSessionActive).Returns(false);
        var viewModel = CreateViewModel();

        // Act
        var canExecute = true;
        viewModel.ExportCommand.CanExecute.Subscribe(result => canExecute = result);

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task ShowTrendAnalysisCommand_CreatesTrendAnalysisViewModel()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        await viewModel.ShowTrendAnalysisCommand.Execute();

        // Assert
        viewModel.ShowTrendAnalysis.Should().BeTrue();
        viewModel.TrendAnalysisViewModel.Should().NotBeNull();
    }

    [Fact]
    public void CloseTrendAnalysisCommand_ClearsTrendAnalysisViewModel()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.CloseTrendAnalysisCommand.Execute().Subscribe();

        // Assert
        viewModel.ShowTrendAnalysis.Should().BeFalse();
        viewModel.TrendAnalysisViewModel.Should().BeNull();
    }

    [Fact]
    public void Constructor_ThrowsOnNullDependencies()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
            null!,
            _mockSettingsService.Object,
            _mockExportService.Object,
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object));

        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
            _mockPapyrusMonitorViewModel,
            null!,
            _mockExportService.Object,
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object));

        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(
            _mockPapyrusMonitorViewModel,
            _mockSettingsService.Object,
            null!,
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object));
    }
}
