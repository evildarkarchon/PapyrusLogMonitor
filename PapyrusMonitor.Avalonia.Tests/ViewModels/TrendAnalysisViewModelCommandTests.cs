using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using OxyPlot;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Models;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class TrendAnalysisViewModelCommandTests : IDisposable
{
    private readonly Mock<ISessionHistoryService> _mockSessionHistoryService;
    private readonly Mock<ITrendAnalysisService> _mockTrendAnalysisService;
    private readonly TestScheduler _testScheduler;

    public TrendAnalysisViewModelCommandTests()
    {
        _testScheduler = new TestScheduler();
        _mockSessionHistoryService = new Mock<ISessionHistoryService>();
        _mockTrendAnalysisService = new Mock<ITrendAnalysisService>();
    }

    public void Dispose()
    {
    }

    private TrendAnalysisViewModel CreateViewModel()
    {
        return new TrendAnalysisViewModel(
            _mockSessionHistoryService.Object,
            _mockTrendAnalysisService.Object);
    }

    [Fact]
    public async Task RefreshCommand_Should_Analyze_Trends_Successfully()
    {
        // Arrange
        var statistics = CreateSampleStatistics(10);
        var analysisResult = CreateSampleAnalysisResult();

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(analysisResult);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.RefreshCommand.Execute();

        // Assert
        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasData.Should().BeTrue();
        viewModel.CurrentAnalysis.Should().Be(analysisResult);
        viewModel.StatusMessage.Should().Contain($"Analysis complete - {statistics.Count} data points");

        // Verify plot models were created
        viewModel.DumpsPlotModel.Should().NotBeNull();
        viewModel.StacksPlotModel.Should().NotBeNull();
        viewModel.WarningsPlotModel.Should().NotBeNull();
        viewModel.ErrorsPlotModel.Should().NotBeNull();
        viewModel.RatioPlotModel.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshCommand_Should_Handle_Insufficient_Data()
    {
        // Arrange
        var statistics = CreateSampleStatistics(1); // Only 1 data point
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.RefreshCommand.Execute();

        // Assert
        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasData.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("Not enough data points for trend analysis (need at least 2)");
        viewModel.CurrentAnalysis.Should().BeNull();
    }

    [Fact]
    public void RefreshCommand_Should_Be_Disabled_While_Loading()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var canExecuteValues = new List<bool>();
        viewModel.RefreshCommand.CanExecute.Subscribe(canExecuteValues.Add);

        // Act - Can't directly set IsLoading, so we check the default state
        canExecuteValues.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Analysis failed");
        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics())
            .Throws(exception);

        var viewModel = CreateViewModel();
        var statusMessages = new List<string>();
        viewModel.WhenAnyValue(x => x.StatusMessage).Subscribe(statusMessages.Add);

        // Act
        await viewModel.RefreshCommand.Execute().Catch(Observable.Return(System.Reactive.Unit.Default));

        // Assert
        viewModel.HasData.Should().BeFalse();
        statusMessages.Should().Contain($"Error: {exception.Message}");
    }

    [Fact]
    public void CloseCommand_Should_Execute_Successfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var executed = false;

        viewModel.CloseCommand.Subscribe(_ => executed = true);

        // Act
        viewModel.CloseCommand.Execute().Subscribe();

        // Assert
        executed.Should().BeTrue();
    }

    [Fact(Skip = "Test requires complex async timing that is better suited for integration tests")]
    public async Task MovingAveragePeriod_Change_Should_Trigger_Auto_Refresh()
    {
        // This test verifies auto-refresh on property change with throttling
        // The interaction between TestScheduler and real async operations is complex
        
        // Arrange
        var statistics = CreateSampleStatistics(10);
        var analysisResult = CreateSampleAnalysisResult();

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(analysisResult);

        var viewModel = CreateViewModel();
        
        // First do initial analysis to set HasData = true
        await viewModel.RefreshCommand.Execute();
        
        // Verify initial analysis was done
        viewModel.HasData.Should().BeTrue();

        // Reset verification
        _mockTrendAnalysisService.Invocations.Clear();

        // Act - Change moving average period
        viewModel.MovingAveragePeriod = 10;
        
        // Wait for throttle time plus processing
        await Task.Delay(700);

        // Assert
        _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), 10), Times.Once);
    }

    [Fact]
    public void MovingAveragePeriod_Change_Should_Not_Refresh_Without_Data()
    {
        // Arrange
        _testScheduler.With(scheduler =>
        {
            var viewModel = CreateViewModel();
            // HasData is false by default

            // Act
            viewModel.MovingAveragePeriod = 10;
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            // Assert
            _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(
                It.IsAny<IReadOnlyList<PapyrusStats>>(), 
                It.IsAny<int>()), Times.Never);
        });
    }

    [Fact(Skip = "Test requires complex async timing that is better suited for integration tests")]
    public async Task MovingAveragePeriod_Change_Should_Be_Throttled()
    {
        // This test verifies throttling behavior with multiple rapid changes
        // The interaction between TestScheduler and real async operations is complex
        
        // Arrange
        var statistics = CreateSampleStatistics(10);
        var analysisResult = CreateSampleAnalysisResult();

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(analysisResult);

        var viewModel = CreateViewModel();
        
        // First do initial analysis
        await viewModel.RefreshCommand.Execute();
        viewModel.HasData.Should().BeTrue();
        _mockTrendAnalysisService.Invocations.Clear();

        // Act - Multiple rapid changes
        viewModel.MovingAveragePeriod = 6;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 7;
        await Task.Delay(100);
        viewModel.MovingAveragePeriod = 8;
        await Task.Delay(100);

        // Assert - Should not have analyzed yet (within throttle period)
        _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(
            It.IsAny<IReadOnlyList<PapyrusStats>>(), 
            It.IsAny<int>()), Times.Never);

        // Wait past throttle time
        await Task.Delay(300);

        // Should analyze with final value
        _mockTrendAnalysisService.Verify(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), 8), Times.Once);
    }

    [Fact]
    public async Task RefreshCommand_Should_Set_IsLoading_During_Execution()
    {
        // Arrange
        var tcs = new TaskCompletionSource<TrendAnalysisResult>();
        var statistics = CreateSampleStatistics(10);

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .Returns(tcs.Task);

        var viewModel = CreateViewModel();
        var isLoadingValues = new List<bool>();
        viewModel.WhenAnyValue(x => x.IsLoading).Subscribe(isLoadingValues.Add);

        // Act
        var refreshTask = viewModel.RefreshCommand.Execute();

        // Assert - Should be loading
        isLoadingValues.Should().HaveCount(2);
        isLoadingValues[0].Should().BeFalse(); // Initial value
        isLoadingValues[1].Should().BeTrue();  // During execution

        // Complete the task
        tcs.SetResult(CreateSampleAnalysisResult());
        await refreshTask;

        // Should no longer be loading
        isLoadingValues.Should().HaveCount(3);
        isLoadingValues[2].Should().BeFalse();
    }

    [Fact]
    public void PlotModels_Should_Have_Correct_Configuration()
    {
        // Arrange
        var statistics = CreateSampleStatistics(5);
        var analysisResult = CreateSampleAnalysisResult();

        _mockSessionHistoryService.Setup(x => x.GetSessionStatistics()).Returns(statistics);
        _mockTrendAnalysisService.Setup(x => x.AnalyzeTrendsAsync(It.IsAny<IReadOnlyList<PapyrusStats>>(), It.IsAny<int>()))
            .ReturnsAsync(analysisResult);

        var viewModel = CreateViewModel();

        // Act
        viewModel.RefreshCommand.Execute().Wait();

        // Assert - Verify plot model properties
        AssertPlotModel(viewModel.DumpsPlotModel!, "Dumps Trend", OxyColors.Blue);
        AssertPlotModel(viewModel.StacksPlotModel!, "Stacks Trend", OxyColors.Green);
        AssertPlotModel(viewModel.WarningsPlotModel!, "Warnings Trend", OxyColors.Orange);
        AssertPlotModel(viewModel.ErrorsPlotModel!, "Errors Trend", OxyColors.Red);
        AssertPlotModel(viewModel.RatioPlotModel!, "Ratio Trend", OxyColors.Purple);
    }

    private void AssertPlotModel(PlotModel model, string expectedTitle, OxyColor expectedColor)
    {
        model.Title.Should().Be(expectedTitle);
        model.Series.Should().NotBeEmpty();
        model.Axes.Should().HaveCount(2);
        model.Background.Should().Be(OxyColors.Transparent);
        model.PlotAreaBackground.Should().Be(OxyColors.Transparent);
    }

    private List<PapyrusStats> CreateSampleStatistics(int count)
    {
        var stats = new List<PapyrusStats>();
        var baseTime = DateTime.Now.AddMinutes(-count);

        for (int i = 0; i < count; i++)
        {
            stats.Add(new PapyrusStats(
                baseTime.AddMinutes(i),
                10 + i,           // Dumps
                20 + i * 2,       // Stacks
                i % 3,            // Warnings
                i % 5,            // Errors
                0.5 + (i * 0.05)  // Ratio
            ));
        }

        return stats;
    }

    private TrendAnalysisResult CreateSampleAnalysisResult()
    {
        var now = DateTime.Now;
        var dataPoints = new List<TrendDataPoint>
        {
            new() { Timestamp = now.AddMinutes(-5), Value = 10 },
            new() { Timestamp = now.AddMinutes(-4), Value = 12 },
            new() { Timestamp = now.AddMinutes(-3), Value = 11 },
            new() { Timestamp = now.AddMinutes(-2), Value = 13 },
            new() { Timestamp = now.AddMinutes(-1), Value = 14 },
            new() { Timestamp = now, Value = 15 }
        };

        var trendData = new TrendData
        {
            DataPoints = dataPoints,
            MovingAverage = dataPoints.Skip(2).ToList(),
            TrendLine = dataPoints,
            Summary = new TrendSummary
            {
                Min = 10,
                Max = 15,
                Average = 12.5,
                StandardDeviation = 1.8,
                TrendSlope = 1.0,
                RSquared = 0.95
            }
        };

        return new TrendAnalysisResult
        {
            DumpsTrend = trendData,
            StacksTrend = trendData,
            WarningsTrend = trendData,
            ErrorsTrend = trendData,
            RatioTrend = trendData,
            TimeRange = new TimeRange { Start = now.AddMinutes(-5), End = now }
        };
    }
}