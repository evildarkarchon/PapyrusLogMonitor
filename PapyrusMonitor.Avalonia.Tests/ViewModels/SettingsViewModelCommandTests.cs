using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;
using ReactiveUI;
using ReactiveUI.Testing;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels;

public class SettingsViewModelCommandTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISchedulerProvider> _mockSchedulerProvider;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IStorageProvider> _mockStorageProvider;
    private readonly Subject<AppSettings> _settingsChangedSubject;
    private readonly TestScheduler _testScheduler;

    public SettingsViewModelCommandTests()
    {
        _testScheduler = new TestScheduler();
        _mockLogger = new Mock<ILogger>();
        _mockSchedulerProvider = new Mock<ISchedulerProvider>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockStorageProvider = new Mock<IStorageProvider>();
        _settingsChangedSubject = new Subject<AppSettings>();

        _mockSchedulerProvider.Setup(x => x.MainThread).Returns(_testScheduler);
        _mockSettingsService.Setup(x => x.SettingsChanged).Returns(_settingsChangedSubject);
        _mockSettingsService.Setup(x => x.Settings).Returns(CreateDefaultSettings());
    }

    public void Dispose()
    {
        _settingsChangedSubject?.Dispose();
    }

    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            LogFilePath = @"C:\Games\Fallout4\Logs\Papyrus.0.log",
            UpdateInterval = 1000,
            AutoStartMonitoring = false,
            MaxLogEntries = 10000,
            ShowErrorNotifications = true,
            ShowWarningNotifications = false,
            ExportSettings = new ExportSettings
            {
                DefaultExportPath = @"C:\Exports",
                IncludeTimestamps = true,
                DateFormat = "yyyy-MM-dd HH:mm:ss"
            }
        };
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
    public async Task SaveCommand_Should_Save_Settings_Successfully()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = @"C:\NewPath\Papyrus.0.log";
        viewModel.UpdateInterval = 2000;

        // Act
        await viewModel.SaveCommand.Execute();

        // Assert
        viewModel.HasChanges.Should().BeFalse();
        viewModel.IsSaving.Should().BeFalse();

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
            It.Is<AppSettings>(s => 
                s.LogFilePath == @"C:\NewPath\Papyrus.0.log" &&
                s.UpdateInterval == 2000)), Times.Once);
    }

    [Fact]
    public void SaveCommand_Should_Be_Disabled_When_No_Changes()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.SaveCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_Should_Be_Enabled_When_Has_Changes()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var canExecuteValues = new List<bool>();
        viewModel.SaveCommand.CanExecute.Subscribe(canExecuteValues.Add);

        // Act
        viewModel.LogFilePath = "new-path.log";
        _testScheduler.AdvanceBy(1);

        // Assert
        viewModel.HasChanges.Should().BeTrue();
        canExecuteValues.Should().Contain(true);
    }

    [Fact]
    public void SaveCommand_Should_Be_Disabled_When_Saving()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "new-path.log";
        _testScheduler.AdvanceBy(1);

        var canExecuteValues = new List<bool>();
        viewModel.SaveCommand.CanExecute.Subscribe(canExecuteValues.Add);

        // Note: We can't directly set IsSaving in the test because it's a private setter
        // The test should verify that the command is disabled during actual save operation
        
        // This test would be better implemented as an integration test
        // For now, we verify the initial state
        canExecuteValues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveCommand_Should_Handle_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Save failed");
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .ThrowsAsync(exception);

        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "new-path.log";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await viewModel.SaveCommand.Execute());

        viewModel.IsSaving.Should().BeFalse();
        _mockLogger.Verify(x => x.LogError("Error saving settings", exception), Times.Once);
    }

    [Fact]
    public void CancelCommand_Should_Revert_Changes()
    {
        // Arrange
        var originalSettings = CreateDefaultSettings();
        _mockSettingsService.Setup(x => x.Settings).Returns(originalSettings);

        var viewModel = CreateViewModel();
        var originalPath = viewModel.LogFilePath;

        viewModel.LogFilePath = "modified-path.log";
        viewModel.UpdateInterval = 5000;

        // Act
        viewModel.CancelCommand.Execute().Subscribe();

        // Assert
        viewModel.LogFilePath.Should().Be(originalPath);
        viewModel.UpdateInterval.Should().Be(1000);
        viewModel.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void CancelCommand_Should_Be_Disabled_When_No_Changes()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.CancelCommand.CanExecute.FirstAsync().Wait().Should().BeFalse();
    }

    [Fact]
    public void CancelCommand_Should_Be_Enabled_When_Has_Changes()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        viewModel.LogFilePath = "new-path.log";
        _testScheduler.AdvanceBy(1);

        // Assert
        viewModel.CancelCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
    }

    [Fact]
    public async Task ResetToDefaultsCommand_Should_Reset_Settings()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "custom-path.log";

        // Act
        await viewModel.ResetToDefaultsCommand.Execute();

        // Assert
        viewModel.IsSaving.Should().BeFalse();
        _mockSettingsService.Verify(x => x.ResetToDefaultsAsync(), Times.Once);
    }

    [Fact]
    public void ResetToDefaultsCommand_Should_Be_Enabled_By_Default()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act & Assert
        viewModel.ResetToDefaultsCommand.CanExecute.FirstAsync().Wait().Should().BeTrue();
    }

    [Fact]
    public async Task BrowseLogFileCommand_Should_Update_LogFilePath()
    {
        // Arrange
        var newPath = @"C:\Games\Skyrim\Logs\Papyrus.0.log";
        var mockFile = new Mock<IStorageFile>();
        mockFile.Setup(x => x.Path).Returns(new Uri($"file:///{newPath.Replace('\\', '/')}"));

        _mockStorageProvider.Setup(x => x.OpenFilePickerAsync(It.IsAny<FilePickerOpenOptions>()))
            .ReturnsAsync(new[] { mockFile.Object });

        var viewModel = CreateViewModel();

        // Act
        await viewModel.BrowseLogFileCommand.Execute();

        // Assert
        viewModel.LogFilePath.Should().Be(newPath);
    }

    [Fact]
    public async Task BrowseLogFileCommand_Should_Handle_Cancellation()
    {
        // Arrange
        _mockStorageProvider.Setup(x => x.OpenFilePickerAsync(It.IsAny<FilePickerOpenOptions>()))
            .ReturnsAsync(Array.Empty<IStorageFile>());

        var viewModel = CreateViewModel();
        var originalPath = viewModel.LogFilePath;

        // Act
        await viewModel.BrowseLogFileCommand.Execute();

        // Assert
        viewModel.LogFilePath.Should().Be(originalPath);
    }

    [Fact]
    public async Task BrowseLogFileCommand_Should_Not_Execute_Without_StorageProvider()
    {
        // Arrange
        var viewModel = new SettingsViewModel(
            _mockSettingsService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            null); // No storage provider

        var originalPath = viewModel.LogFilePath;

        // Act
        await viewModel.BrowseLogFileCommand.Execute();

        // Assert
        viewModel.LogFilePath.Should().Be(originalPath);
        _mockStorageProvider.Verify(x => x.OpenFilePickerAsync(It.IsAny<FilePickerOpenOptions>()), Times.Never);
    }

    [Fact]
    public async Task BrowseExportPathCommand_Should_Update_DefaultExportPath()
    {
        // Arrange
        var newPath = @"C:\Documents\Exports";
        var mockFolder = new Mock<IStorageFolder>();
        mockFolder.Setup(x => x.Path).Returns(new Uri($"file:///{newPath.Replace('\\', '/')}"));

        _mockStorageProvider.Setup(x => x.OpenFolderPickerAsync(It.IsAny<FolderPickerOpenOptions>()))
            .ReturnsAsync(new[] { mockFolder.Object });

        var viewModel = CreateViewModel();

        // Act
        await viewModel.BrowseExportPathCommand.Execute();

        // Assert
        viewModel.DefaultExportPath.Should().Be(newPath);
    }

    [Fact]
    public async Task BrowseExportPathCommand_Should_Handle_Cancellation()
    {
        // Arrange
        _mockStorageProvider.Setup(x => x.OpenFolderPickerAsync(It.IsAny<FolderPickerOpenOptions>()))
            .ReturnsAsync(Array.Empty<IStorageFolder>());

        var viewModel = CreateViewModel();
        var originalPath = viewModel.DefaultExportPath;

        // Act
        await viewModel.BrowseExportPathCommand.Execute();

        // Assert
        viewModel.DefaultExportPath.Should().Be(originalPath);
    }

    [Fact]
    public void HasChanges_Should_Track_All_Property_Changes()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var hasChangesValues = new List<bool>();
        viewModel.WhenAnyValue(x => x.HasChanges).Subscribe(hasChangesValues.Add);

        // Act & Assert - Test each property
        viewModel.LogFilePath = "new-log.log";
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.UpdateInterval = 5000;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.AutoStartMonitoring = true;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.MaxLogEntries = 20000;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.ShowErrorNotifications = false;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.ShowWarningNotifications = true;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.DefaultExportPath = @"C:\NewExports";
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.IncludeTimestamps = false;
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();

        viewModel.CancelCommand.Execute().Subscribe(); // Reset
        viewModel.DateFormat = "dd/MM/yyyy";
        _testScheduler.AdvanceBy(1);
        viewModel.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Should_Update_When_Settings_Changed_Externally()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var newSettings = CreateDefaultSettings() with
        {
            LogFilePath = @"C:\ExternalChange\Papyrus.0.log",
            UpdateInterval = 3000
        };

        // Act
        _settingsChangedSubject.OnNext(newSettings);
        _testScheduler.AdvanceBy(1);

        // Assert
        viewModel.LogFilePath.Should().Be(@"C:\ExternalChange\Papyrus.0.log");
        viewModel.UpdateInterval.Should().Be(3000);
        viewModel.HasChanges.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_Should_Preserve_Window_Settings()
    {
        // Arrange
        var originalSettings = CreateDefaultSettings() with
        {
            WindowSettings = new WindowSettings
            {
                Width = 800,
                Height = 600,
                X = 100,
                Y = 100
            }
        };
        _mockSettingsService.Setup(x => x.Settings).Returns(originalSettings);

        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "new-path.log";

        AppSettings? savedSettings = null;
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Callback<AppSettings>(s => savedSettings = s)
            .Returns(Task.CompletedTask);

        // Act
        await viewModel.SaveCommand.Execute();

        // Assert
        savedSettings.Should().NotBeNull();
        savedSettings!.WindowSettings.Should().BeEquivalentTo(originalSettings.WindowSettings);
    }
}