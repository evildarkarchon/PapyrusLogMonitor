using System.Reactive;
using System.Reactive.Linq;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using PapyrusMonitor.Core.Analytics;
using PapyrusMonitor.Core.Interfaces;
using PapyrusMonitor.Core.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class TrendAnalysisViewModel : ViewModelBase
{
    private readonly ISessionHistoryService _sessionHistoryService;
    private readonly ITrendAnalysisService _trendAnalysisService;

    public TrendAnalysisViewModel(
        ISessionHistoryService sessionHistoryService,
        ITrendAnalysisService trendAnalysisService)
    {
        _sessionHistoryService = sessionHistoryService;
        _trendAnalysisService = trendAnalysisService;

        var canRefresh = this.WhenAnyValue(x => x.IsLoading, loading => !loading);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAnalysisAsync, canRefresh);

        CloseCommand = ReactiveCommand.Create(() => { });

        // Auto-refresh when moving average period changes
        this.WhenAnyValue(x => x.MovingAveragePeriod)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Where(_ => HasData)
            .SelectMany(_ => RefreshCommand.Execute())
            .Subscribe();

        // Handle errors
        RefreshCommand.ThrownExceptions.Subscribe(ex =>
        {
            StatusMessage = $"Error: {ex.Message}";
            HasData = false;
        });
    }

    [Reactive] public bool IsLoading { get; private set; }
    [Reactive] public bool HasData { get; private set; }
    [Reactive] public string StatusMessage { get; private set; } = "No data to analyze";

    [Reactive] public PlotModel? DumpsPlotModel { get; private set; }
    [Reactive] public PlotModel? StacksPlotModel { get; private set; }
    [Reactive] public PlotModel? WarningsPlotModel { get; private set; }
    [Reactive] public PlotModel? ErrorsPlotModel { get; private set; }
    [Reactive] public PlotModel? RatioPlotModel { get; private set; }

    [Reactive] public TrendAnalysisResult? CurrentAnalysis { get; private set; }
    [Reactive] public int MovingAveragePeriod { get; set; } = 5;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    private async Task RefreshAnalysisAsync()
    {
        IsLoading = true;
        try
        {
            var statistics = _sessionHistoryService.GetSessionStatistics();

            if (statistics.Count < 2)
            {
                StatusMessage = "Not enough data points for trend analysis (need at least 2)";
                HasData = false;
                return;
            }

            StatusMessage = $"Analyzing {statistics.Count} data points...";

            // Perform trend analysis
            CurrentAnalysis = await _trendAnalysisService.AnalyzeTrendsAsync(
                statistics,
                MovingAveragePeriod);

            // Create plot models
            DumpsPlotModel = CreatePlotModel("Dumps Trend", CurrentAnalysis.DumpsTrend, OxyColors.Blue);
            StacksPlotModel = CreatePlotModel("Stacks Trend", CurrentAnalysis.StacksTrend, OxyColors.Green);
            WarningsPlotModel = CreatePlotModel("Warnings Trend", CurrentAnalysis.WarningsTrend, OxyColors.Orange);
            ErrorsPlotModel = CreatePlotModel("Errors Trend", CurrentAnalysis.ErrorsTrend, OxyColors.Red);
            RatioPlotModel = CreatePlotModel("Ratio Trend", CurrentAnalysis.RatioTrend, OxyColors.Purple);

            StatusMessage =
                $"Analysis complete - {statistics.Count} data points over {CurrentAnalysis.TimeRange.Duration:hh\\:mm\\:ss}";
            HasData = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private PlotModel CreatePlotModel(string title, TrendData trendData, OxyColor color)
    {
        var model = new PlotModel
        {
            Title = title,
            Background = OxyColors.Transparent,
            PlotAreaBackground = OxyColors.Transparent,
            TextColor = OxyColors.Black,
            TitleFontSize = 14,
            DefaultFont = "Segoe UI"
        };

        // Add axes
        var dateAxis = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            Title = "Time",
            TitleFontSize = 12,
            FontSize = 10
        };
        model.Axes.Add(dateAxis);

        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Value",
            TitleFontSize = 12,
            FontSize = 10,
            Minimum = 0
        };
        model.Axes.Add(valueAxis);

        // Add data series
        if (trendData.DataPoints.Count > 0)
        {
            // Raw data
            var dataSeries = new LineSeries
            {
                Title = "Actual",
                Color = color,
                StrokeThickness = 1,
                MarkerSize = 3,
                MarkerType = MarkerType.Circle,
                MarkerFill = color
            };

            foreach (var point in trendData.DataPoints)
            {
                dataSeries.Points.Add(DateTimeAxis.CreateDataPoint(point.Timestamp, point.Value));
            }

            model.Series.Add(dataSeries);

            // Moving average
            if (trendData.MovingAverage.Count > 0)
            {
                var maSeries = new LineSeries
                {
                    Title = $"Moving Avg ({MovingAveragePeriod})",
                    Color = OxyColor.FromAColor(180, color),
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Solid
                };

                foreach (var point in trendData.MovingAverage)
                {
                    maSeries.Points.Add(DateTimeAxis.CreateDataPoint(point.Timestamp, point.Value));
                }

                model.Series.Add(maSeries);
            }

            // Trend line
            if (trendData.TrendLine.Count > 0)
            {
                var trendSeries = new LineSeries
                {
                    Title = $"Trend (R²={trendData.Summary.RSquared:F3})",
                    Color = OxyColors.DarkGray,
                    StrokeThickness = 2,
                    LineStyle = LineStyle.Dash
                };

                foreach (var point in trendData.TrendLine)
                {
                    trendSeries.Points.Add(DateTimeAxis.CreateDataPoint(point.Timestamp, point.Value));
                }

                model.Series.Add(trendSeries);
            }
        }

        // Add statistics annotation
        {
            var annotation = new TextAnnotation
            {
                Text =
                    $"Min: {trendData.Summary.Min:F1}, Max: {trendData.Summary.Max:F1}, Avg: {trendData.Summary.Average:F1}",
                TextPosition = new DataPoint(DateTimeAxis.ToDouble(trendData.DataPoints.Last().Timestamp),
                    trendData.Summary.Max * 1.1),
                FontSize = 10,
                TextHorizontalAlignment = HorizontalAlignment.Right
            };
            model.Annotations.Add(annotation);
        }

        return model;
    }
}
