using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Platform.Storage;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly AppSettings _defaultSettings;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISchedulerProvider> _mockSchedulerProvider;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IStorageProvider> _mockStorageProvider;
    private readonly Subject<AppSettings> _settingsChangedSubject;
    private readonly TestScheduler _testScheduler;

    public SettingsViewModelTests()
    {
        _testScheduler = new TestScheduler();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSchedulerProvider = new Mock<ISchedulerProvider>();
        _mockLogger = new Mock<ILogger>();
        _mockStorageProvider = new Mock<IStorageProvider>();
        _settingsChangedSubject = new Subject<AppSettings>();

        // Setup default settings
        _defaultSettings = new AppSettings
        {
            LogFilePath = "test.log",
            UpdateInterval = 1000,
            AutoStartMonitoring = false,
            MaxLogEntries = 10000,
            ShowErrorNotifications = true,
            ShowWarningNotifications = false,
            ExportSettings = new ExportSettings
            {
                DefaultExportPath = "exports", IncludeTimestamps = true, DateFormat = "yyyy-MM-dd HH:mm:ss"
            },
            WindowSettings = new WindowSettings()
        };

        // Setup mock behaviors
        _mockSettingsService.Setup(x => x.Settings).Returns(_defaultSettings);
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(_settingsChangedSubject);
        _mockSchedulerProvider.Setup(x => x.MainThread).Returns(_testScheduler);
        _mockSchedulerProvider.Setup(x => x.TaskPool).Returns(_testScheduler);
        _mockSchedulerProvider.Setup(x => x.CurrentThread).Returns(_testScheduler);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _mockSettingsService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object);
    }

    [Fact]
    public void Constructor_LoadsCurrentSettings()
    {
        // Act
        var viewModel = CreateViewModel();

        // Assert
        Assert.Equal(_defaultSettings.LogFilePath, viewModel.LogFilePath);
        Assert.Equal(_defaultSettings.UpdateInterval, viewModel.UpdateInterval);
        Assert.Equal(_defaultSettings.AutoStartMonitoring, viewModel.AutoStartMonitoring);
        Assert.Equal(_defaultSettings.MaxLogEntries, viewModel.MaxLogEntries);
        Assert.Equal(_defaultSettings.ShowErrorNotifications, viewModel.ShowErrorNotifications);
        Assert.Equal(_defaultSettings.ShowWarningNotifications, viewModel.ShowWarningNotifications);
        Assert.Equal(_defaultSettings.ExportSettings.DefaultExportPath, viewModel.DefaultExportPath);
        Assert.Equal(_defaultSettings.ExportSettings.IncludeTimestamps, viewModel.IncludeTimestamps);
        Assert.Equal(_defaultSettings.ExportSettings.DateFormat, viewModel.DateFormat);
        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void SettingsChanged_UpdatesViewModel()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var newSettings = new AppSettings
        {
            LogFilePath = "new.log",
            UpdateInterval = 2000,
            AutoStartMonitoring = true,
            MaxLogEntries = 5000,
            ShowErrorNotifications = false,
            ShowWarningNotifications = true,
            ExportSettings = new ExportSettings
            {
                DefaultExportPath = "new_exports", IncludeTimestamps = false, DateFormat = "dd/MM/yyyy"
            },
            WindowSettings = new WindowSettings()
        };

        // Act
        _settingsChangedSubject.OnNext(newSettings);
        _testScheduler.AdvanceBy(1);

        // Assert
        Assert.Equal(newSettings.LogFilePath, viewModel.LogFilePath);
        Assert.Equal(newSettings.UpdateInterval, viewModel.UpdateInterval);
        Assert.Equal(newSettings.AutoStartMonitoring, viewModel.AutoStartMonitoring);
        Assert.Equal(newSettings.MaxLogEntries, viewModel.MaxLogEntries);
        Assert.Equal(newSettings.ShowErrorNotifications, viewModel.ShowErrorNotifications);
        Assert.Equal(newSettings.ShowWarningNotifications, viewModel.ShowWarningNotifications);
        Assert.Equal(newSettings.ExportSettings.DefaultExportPath, viewModel.DefaultExportPath);
        Assert.Equal(newSettings.ExportSettings.IncludeTimestamps, viewModel.IncludeTimestamps);
        Assert.Equal(newSettings.ExportSettings.DateFormat, viewModel.DateFormat);
        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public void PropertyChange_SetsHasChanges()
    {
        // Arrange
        var viewModel = CreateViewModel();
        _testScheduler.AdvanceBy(1); // Skip initial values

        // Act
        viewModel.LogFilePath = "modified.log";
        _testScheduler.AdvanceBy(1);

        // Assert
        Assert.True(viewModel.HasChanges);
    }

    [Fact]
    public async Task SaveCommand_CallsSettingsService()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "modified.log";
        viewModel.UpdateInterval = 3000;

        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(Task.CompletedTask);

        // Act
        await viewModel.SaveCommand.Execute();

        // Assert
        _mockSettingsService.Verify(x => x.SaveSettingsAsync(It.Is<AppSettings>(s =>
            s.LogFilePath == "modified.log" &&
            s.UpdateInterval == 3000)), Times.Once);
        Assert.False(viewModel.HasChanges);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task SaveCommand_HandlesErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "modified.log";

        var exception = new Exception("Save failed");
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => await viewModel.SaveCommand.Execute());

        // Verify logger was called
        _mockLogger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Error saving settings")), exception),
            Times.Once);
    }

    [Fact]
    public void CancelCommand_ResetsToOriginalSettings()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "modified.log";
        viewModel.UpdateInterval = 3000;
        Assert.True(viewModel.HasChanges);

        // Act
        viewModel.CancelCommand.Execute().Subscribe();

        // Assert
        Assert.Equal(_defaultSettings.LogFilePath, viewModel.LogFilePath);
        Assert.Equal(_defaultSettings.UpdateInterval, viewModel.UpdateInterval);
        Assert.False(viewModel.HasChanges);
    }

    [Fact]
    public async Task ResetToDefaultsCommand_CallsSettingsService()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var defaultSettings = new AppSettings();

        _mockSettingsService.Setup(x => x.ResetToDefaultsAsync())
            .Returns(Task.CompletedTask);
        _mockSettingsService.SetupSequence(x => x.Settings)
            .Returns(_defaultSettings)
            .Returns(defaultSettings);

        // Act
        await viewModel.ResetToDefaultsCommand.Execute();

        // Assert
        _mockSettingsService.Verify(x => x.ResetToDefaultsAsync(), Times.Once);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task SaveCommand_DisabledWhenNoChanges()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        Assert.False(await viewModel.SaveCommand.CanExecute.FirstAsync());
    }

    [Fact]
    public async Task SaveCommand_EnabledWhenHasChanges()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.LogFilePath = "modified.log";
        _testScheduler.AdvanceBy(1);

        // Assert
        Assert.True(await viewModel.SaveCommand.CanExecute.FirstAsync());
    }

    [Fact]
    public async Task SaveCommand_DisabledWhileSaving()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "modified.log";
        _testScheduler.AdvanceBy(1);

        // Start a save operation
        var tcs = new TaskCompletionSource<Unit>();
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(tcs.Task);

        // Act - start saving (but don't complete)
        var saveTask = viewModel.SaveCommand.Execute();

        // Assert - command should be disabled while saving
        Assert.False(await viewModel.SaveCommand.CanExecute.FirstAsync());

        // Complete the save
        tcs.SetResult(Unit.Default);
        await saveTask;
    }

    [Fact]
    public void Constructor_UsesSchedulerProviderForObservables()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert - verify that the scheduler provider was used
        _mockSchedulerProvider.Verify(x => x.MainThread, Times.AtLeastOnce);
    }
}
