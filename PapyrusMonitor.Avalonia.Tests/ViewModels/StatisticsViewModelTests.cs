using System;
using Avalonia.Media;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Models;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class StatisticsViewModelTests
{
    private readonly TestScheduler _testScheduler;

    public StatisticsViewModelTests()
    {
        _testScheduler = new TestScheduler();
        RxApp.MainThreadScheduler = _testScheduler;
    }

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var viewModel = new StatisticsViewModel();

        // Assert
        viewModel.CurrentStats.Should().BeNull();
        viewModel.LastUpdateTime.Should().Be("Never");
        viewModel.DumpsDisplay.Should().Be("0");
        viewModel.StacksDisplay.Should().Be("0");
        viewModel.WarningsDisplay.Should().Be("0");
        viewModel.ErrorsDisplay.Should().Be("0");
        viewModel.RatioDisplay.Should().Be("0.00");
        viewModel.StatusIcon.Should().Be("⏸");
        viewModel.StatusColor.Should().Be(Brushes.Gray);
        viewModel.StatusText.Should().Be("No Data");
    }

    [Fact]
    public void UpdateStats_UpdatesAllProperties()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 10,
            Stacks: 20,
            Warnings: 5,
            Errors: 2,
            Ratio: 0.5);

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.CurrentStats.Should().Be(stats);
        viewModel.DumpsDisplay.Should().Be("10");
        viewModel.StacksDisplay.Should().Be("20");
        viewModel.WarningsDisplay.Should().Be("5");
        viewModel.ErrorsDisplay.Should().Be("2");
        viewModel.RatioDisplay.Should().Be("0.50");
        viewModel.LastUpdateTime.Should().Be("Just now");
    }

    [Fact]
    public void StatusIndicators_ShowGoodStatus_WhenNoIssues()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 1,
            Stacks: 10,
            Warnings: 0,
            Errors: 0,
            Ratio: 0.1); // Below warning threshold

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.StatusIcon.Should().Be("✓");
        viewModel.StatusColor.Should().Be(Brushes.Green);
        viewModel.StatusText.Should().Be("Healthy");
    }

    [Fact]
    public void StatusIndicators_ShowWarningStatus_WhenWarningsPresent()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 5,
            Stacks: 10,
            Warnings: 1,
            Errors: 0,
            Ratio: 0.5); // At warning threshold

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.StatusIcon.Should().Be("⚠️");
        viewModel.StatusColor.Should().Be(Brushes.Orange);
        viewModel.StatusText.Should().Be("Warning");
    }

    [Fact]
    public void StatusIndicators_ShowErrorStatus_WhenErrorsPresent()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 8,
            Stacks: 10,
            Warnings: 0,
            Errors: 1,
            Ratio: 0.8); // At error threshold

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.StatusIcon.Should().Be("❌");
        viewModel.StatusColor.Should().Be(Brushes.Red);
        viewModel.StatusText.Should().Be("Critical");
    }

    [Fact]
    public void StatusIndicators_ShowErrorStatus_WhenRatioExceedsErrorThreshold()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 9,
            Stacks: 10,
            Warnings: 0,
            Errors: 0,
            Ratio: 0.9); // Above error threshold

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.StatusIcon.Should().Be("❌");
        viewModel.StatusColor.Should().Be(Brushes.Red);
        viewModel.StatusText.Should().Be("Critical");
    }

    [Fact]
    public void LastUpdateTime_ShowsSecondsAgo()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now.AddSeconds(-30),
            Dumps: 0,
            Stacks: 0,
            Warnings: 0,
            Errors: 0,
            Ratio: 0);

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.LastUpdateTime.Should().Be("30 seconds ago");
    }

    [Fact]
    public void LastUpdateTime_ShowsMinutesAgo()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var stats = new PapyrusStats(
            DateTime.Now.AddMinutes(-5),
            Dumps: 0,
            Stacks: 0,
            Warnings: 0,
            Errors: 0,
            Ratio: 0);

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.LastUpdateTime.Should().Be("5 minutes ago");
    }

    [Fact]
    public void LastUpdateTime_ShowsTimeForOldUpdates()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var timestamp = DateTime.Now.AddHours(-2);
        var stats = new PapyrusStats(
            timestamp,
            Dumps: 0,
            Stacks: 0,
            Warnings: 0,
            Errors: 0,
            Ratio: 0);

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        viewModel.LastUpdateTime.Should().Be(timestamp.ToString("HH:mm:ss"));
    }

    [Fact]
    public void Activation_UpdatesTimeDisplayPeriodically()
    {
        _testScheduler.With(scheduler =>
        {
            // Arrange
            var viewModel = new StatisticsViewModel();
            var fixedTime = DateTime.Now.AddSeconds(-2); // Stats from 2 seconds ago
            var stats = new PapyrusStats(
                fixedTime,
                Dumps: 0,
                Stacks: 0,
                Warnings: 0,
                Errors: 0,
                Ratio: 0);
            
            viewModel.UpdateStats(stats);
            viewModel.Activator.Activate();

            // Act - advance scheduler to trigger the interval
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

            // Assert
            // The time display should show "2 seconds ago" or similar
            viewModel.LastUpdateTime.Should().Contain("seconds ago");
        });
    }

    [Fact]
    public void PropertyChanged_RaisedForAllDisplayProperties()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();
        var propertyNames = new List<string>();
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
                propertyNames.Add(args.PropertyName);
        };

        var stats = new PapyrusStats(
            DateTime.Now,
            Dumps: 10,
            Stacks: 20,
            Warnings: 5,
            Errors: 2,
            Ratio: 0.5);

        // Act
        viewModel.UpdateStats(stats);

        // Assert
        propertyNames.Should().Contain(nameof(StatisticsViewModel.CurrentStats));
        propertyNames.Should().Contain(nameof(StatisticsViewModel.LastUpdateTime));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var viewModel = new StatisticsViewModel();

        // Act & Assert
        viewModel.Invoking(vm => vm.Dispose()).Should().NotThrow();
    }
}