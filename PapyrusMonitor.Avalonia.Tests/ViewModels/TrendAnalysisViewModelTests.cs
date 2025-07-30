using System.Reactive;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class TrendAnalysisViewModelTests
{
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly Mock<ITrendAnalysisService> _mockTrendAnalysisService;
    private readonly TestScheduler _testScheduler;

    public TrendAnalysisViewModelTests()
    {
        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _mockTrendAnalysisService = new Mock<ITrendAnalysisService>();
        _testScheduler = new TestScheduler();
    }

    private TrendAnalysisViewModel CreateViewModel()
    {
        return new TrendAnalysisViewModel(
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object);
    }

    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasData.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("No data to analyze");
        viewModel.MovingAveragePeriod.Should().Be(5);
        viewModel.CurrentAnalysis.Should().BeNull();
        viewModel.DumpsPlotModel.Should().BeNull();
        viewModel.StacksPlotModel.Should().BeNull();
        viewModel.WarningsPlotModel.Should().BeNull();
        viewModel.ErrorsPlotModel.Should().BeNull();
        viewModel.RatioPlotModel.Should().BeNull();
    }

    [Fact]
    public void RefreshCommand_CanExecute_WhenNotLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();
        // IsLoading is initially false

        // Act & Assert
        viewModel.RefreshCommand.CanExecute
            .FirstAsync()
            .Wait();
        
        viewModel.RefreshCommand.CanExecute
            .FirstAsync()
            .Subscribe(canExecute => canExecute.Should().BeTrue());
    }

    [Fact]
    public async Task RefreshCommand_CannotExecute_WhenLoading()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var statistics = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1),
            new(DateTime.Now.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return CreateTestAnalysisResult(DateTime.Now);
            });

        var canExecuteStates = new List<bool>();
        viewModel.RefreshCommand.CanExecute.Subscribe(canExecuteStates.Add);

        // Act
        var refreshTask = viewModel.RefreshCommand.Execute();
        await Task.Delay(50); // Let loading state set

        // Assert - Should be false while loading
        canExecuteStates.Should().Contain(false);
        
        await refreshTask;
    }

    [Fact]
    public async Task RefreshCommand_WithInsufficientData_ShowsWarningMessage()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics())
            .Returns(new List<PapyrusStats> { new(DateTime.Now, 10, 5, 2, 1, 0.1) });

        // Act
        await viewModel.RefreshCommand.Execute();

        // Assert
        viewModel.StatusMessage.Should().Be("Not enough data points for trend analysis (need at least 2)");
        viewModel.HasData.Should().BeFalse();
        viewModel.CurrentAnalysis.Should().BeNull();
        _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(
            It.IsAny<IReadOnlyList<PapyrusStats>>(), 
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RefreshCommand_WithSufficientData_PerformsAnalysis()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var baseTime = DateTime.Now;
        var statistics = new List<PapyrusStats>
        {
            new(baseTime, 10, 5, 2, 1, 0.1),
            new(baseTime.AddMinutes(1), 20, 10, 4, 2, 0.2),
            new(baseTime.AddMinutes(2), 30, 15, 6, 3, 0.3)
        };

        var analysisResult = CreateTestAnalysisResult(baseTime);

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(statistics, 5))
            .ReturnsAsync(analysisResult);

        // Act
        await viewModel.RefreshCommand.Execute();

        // Assert
        viewModel.StatusMessage.Should().Contain("Analysis complete - 3 data points");
        viewModel.HasData.Should().BeTrue();
        viewModel.CurrentAnalysis.Should().Be(analysisResult);
        viewModel.DumpsPlotModel.Should().NotBeNull();
        viewModel.StacksPlotModel.Should().NotBeNull();
        viewModel.WarningsPlotModel.Should().NotBeNull();
        viewModel.ErrorsPlotModel.Should().NotBeNull();
        viewModel.RatioPlotModel.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshCommand_SetsLoadingStateCorrectly()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var loadingStates = new List<bool>();
        viewModel.WhenAnyValue(x => x.IsLoading).Subscribe(loadingStates.Add);

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics())
            .Returns(new List<PapyrusStats>());

        // Act
        await viewModel.RefreshCommand.Execute();

        // Assert
        loadingStates.Should().ContainInOrder(false, true, false);
    }

    [Fact]
    public async Task RefreshCommand_WithException_HandlesError()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var statistics = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1),
            new(DateTime.Now.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("Analysis failed"));

        // Act
        try
        {
            await viewModel.RefreshCommand.Execute();
        }
        catch
        {
            // Expected exception
        }

        // Assert
        viewModel.StatusMessage.Should().Contain("Error: Analysis failed");
        viewModel.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task MovingAveragePeriod_Change_TriggersRefresh_WhenHasData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        var statistics = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1),
            new(DateTime.Now.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(CreateTestAnalysisResult(DateTime.Now));

        // First refresh to set HasData = true
        await viewModel.RefreshCommand.Execute();
        viewModel.HasData.Should().BeTrue();

        // Reset the mock to track new calls
        _mockTrendAnalysisService.Invocations.Clear();

        // Act - Change moving average period
        viewModel.MovingAveragePeriod = 10;
        
        // Wait for throttle period and command execution
        await Task.Delay(700);

        // Assert
        _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), 10), Times.Once);
    }

    [Fact]
    public void MovingAveragePeriod_Change_DoesNotTriggerRefresh_WhenNoData()
    {
        // Arrange
        _testScheduler.With(scheduler =>
        {
            var viewModel = CreateViewModel();
            // HasData is initially false

            var refreshCount = 0;
            viewModel.RefreshCommand.Subscribe(_ => refreshCount++);

            // Act
            viewModel.MovingAveragePeriod = 10;
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            // Assert
            refreshCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task MovingAveragePeriod_RapidChanges_ThrottlesRefresh()
    {
        // Arrange
        var viewModel = CreateViewModel();

        var statistics = new List<PapyrusStats>
        {
            new(DateTime.Now, 10, 5, 2, 1, 0.1),
            new(DateTime.Now.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        
        var callCount = 0;
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return CreateTestAnalysisResult(DateTime.Now);
            });

        // First refresh to set HasData = true
        await viewModel.RefreshCommand.Execute();
        callCount = 0; // Reset count

        // Act - Rapid changes
        viewModel.MovingAveragePeriod = 6;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 7;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 8;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 9;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 10;
        
        // Wait for throttle period to expire
        await Task.Delay(700);

        // Assert - Should only refresh once due to throttling
        callCount.Should().Be(1);
    }

    [Fact]
    public void PlotModels_ContainCorrectData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var baseTime = DateTime.Now;
        var statistics = new List<PapyrusStats>
        {
            new(baseTime, 10, 5, 2, 1, 0.1),
            new(baseTime.AddMinutes(1), 20, 10, 4, 2, 0.2)
        };

        var analysisResult = CreateTestAnalysisResult(baseTime);
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(statistics, 5))
            .ReturnsAsync(analysisResult);

        // Act
        viewModel.RefreshCommand.Execute().Wait();

        // Assert
        viewModel.DumpsPlotModel.Should().NotBeNull();
        viewModel.DumpsPlotModel!.Title.Should().Be("Dumps Trend");
        viewModel.DumpsPlotModel.Series.Should().HaveCount(3); // Actual, Moving Avg, Trend
        viewModel.DumpsPlotModel.Axes.Should().HaveCount(2); // Time and Value axes
        viewModel.DumpsPlotModel.Annotations.Should().HaveCount(1); // Statistics annotation

        // Verify all plot models are created
        viewModel.StacksPlotModel!.Title.Should().Be("Stacks Trend");
        viewModel.WarningsPlotModel!.Title.Should().Be("Warnings Trend");
        viewModel.ErrorsPlotModel!.Title.Should().Be("Errors Trend");
        viewModel.RatioPlotModel!.Title.Should().Be("Ratio Trend");
    }

    [Fact]
    public void CloseCommand_CanAlwaysExecute()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.CloseCommand.CanExecute
            .FirstAsync()
            .Subscribe(canExecute => canExecute.Should().BeTrue());
    }

    private static TrendAnalysisResult CreateTestAnalysisResult(DateTime baseTime)
    {
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = baseTime, Value = 10 },
            new() { Timestamp = baseTime.AddMinutes(1), Value = 20 }
        };

        var trendData = new TrendData
        {
            DataPoints = dataPoints,
            MovingAverage = dataPoints,
            TrendLine = dataPoints,
            Summary = new TrendSummary
            {
                Min = 10,
                Max = 20,
                Average = 15,
                StandardDeviation = 5,
                TrendSlope = 10,
                RSquared = 1.0
            }
        };

        return new TrendAnalysisResult
        {
            DumpsTrend = trendData,
            StacksTrend = trendData,
            WarningsTrend = trendData,
            ErrorsTrend = trendData,
            RatioTrend = trendData,
            TimeRange = new TimeRange { Start = baseTime, End = baseTime.AddMinutes(1) }
        };
    }
}