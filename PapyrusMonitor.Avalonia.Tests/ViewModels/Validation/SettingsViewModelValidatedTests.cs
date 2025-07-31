using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Platform.Storage;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Moq;
using PapyrusMonitor.Avalonia.ViewModels;
using PapyrusMonitor.Core.Configuration;
using PapyrusMonitor.Core.Interfaces;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels.Validation;

/// <summary>
/// Comprehensive tests for SettingsViewModelValidated covering validation scenarios
/// specific to settings management in the PapyrusLogMonitor application.
/// </summary>
public class SettingsViewModelValidatedTests : IDisposable
{
    private readonly AppSettings _defaultSettings;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISchedulerProvider> _mockSchedulerProvider;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IStorageProvider> _mockStorageProvider;
    private readonly Subject<AppSettings> _settingsChangedSubject;
    private readonly TestScheduler _testScheduler;
    private readonly string _tempDirectory;
    private readonly string _tempLogFile;

    public SettingsViewModelValidatedTests()
    {
        _testScheduler = new TestScheduler();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSchedulerProvider = new Mock<ISchedulerProvider>();
        _mockLogger = new Mock<ILogger>();
        _mockStorageProvider = new Mock<IStorageProvider>();
        _settingsChangedSubject = new Subject<AppSettings>();

        // Create temporary files for testing
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _tempLogFile = Path.Combine(_tempDirectory, "Papyrus.0.log");
        File.WriteAllText(_tempLogFile, "Test log content");

        // Setup default settings
        _defaultSettings = new AppSettings
        {
            LogFilePath = _tempLogFile,
            UpdateInterval = 1000,
            AutoStartMonitoring = false,
            MaxLogEntries = 10000,
            ShowErrorNotifications = true,
            ShowWarningNotifications = false,
            ExportSettings = new ExportSettings
            {
                DefaultExportPath = _tempDirectory,
                IncludeTimestamps = true,
                DateFormat = "yyyy-MM-dd HH:mm:ss"
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

    public void Dispose()
    {
        _settingsChangedSubject?.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private SettingsViewModelValidated CreateViewModel()
    {
        return new SettingsViewModelValidated(
            _mockSettingsService.Object,
            _mockSchedulerProvider.Object,
            _mockLogger.Object,
            _mockStorageProvider.Object);
    }

    private async Task WithActivatedViewModel(SettingsViewModelValidated viewModel, Func<SettingsViewModelValidated, Task> testAction)
    {
        viewModel.Activator.Activate();
        try
        {
            await testAction(viewModel);
        }
        finally
        {
            viewModel.Activator.Deactivate();
        }
    }

    #region Basic Validation Tests

    [Fact]
    public async Task LogFilePath_RequiredValidation_FailsForEmptyPath()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        await WithActivatedViewModel(viewModel, async vm =>
        {
            // Act
            vm.LogFilePath = "";
            await vm.ValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath), "");

            // Assert
            vm.HasErrors.Should().BeTrue();
            var errors = vm.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
            errors.Should().Contain("Log file path is required.");
        });
    }

    [Fact]
    public async Task LogFilePath_ExtensionValidation_FailsForNonLogFile()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.LogFilePath = "C:\\test\\file.txt";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().Contain("Log file must have a .log extension.");
    }

    [Fact]
    public async Task LogFilePath_AsyncValidation_WarnsForNonexistentFile()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.LogFilePath = "C:\\nonexistent\\directory\\that\\does\\not\\exist\\Papyrus.0.log";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().Contain("Log file directory does not exist.");
    }

    [Fact]
    public async Task LogFilePath_AsyncValidation_PassesForExistingFile()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.LogFilePath = _tempLogFile;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateInterval_RangeValidation_FailsForTooSmallValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.UpdateInterval = 50;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval));
        errors.Should().Contain("Update interval must be between 100 and 10000 milliseconds.");
    }

    [Fact]
    public async Task UpdateInterval_RangeValidation_FailsForTooLargeValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.UpdateInterval = 15000;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval));
        errors.Should().Contain("Update interval must be between 100 and 10000 milliseconds.");
    }

    [Fact]
    public async Task UpdateInterval_RangeValidation_PassesForValidValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.UpdateInterval = 1000;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval));

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval));
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task MaxLogEntries_RangeValidation_FailsForTooSmallValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.MaxLogEntries = 500;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.MaxLogEntries));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.MaxLogEntries));
        errors.Should().Contain("Max log entries must be between 1000 and 100000.");
    }

    [Fact]
    public async Task MaxLogEntries_RangeValidation_FailsForTooLargeValue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.MaxLogEntries = 150000;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.MaxLogEntries));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.MaxLogEntries));
        errors.Should().Contain("Max log entries must be between 1000 and 100000.");
    }

    [Fact]
    public async Task DateFormat_RequiredValidation_FailsForEmptyFormat()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DateFormat = "";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DateFormat));
        errors.Should().Contain("Date format is required.");
    }

    [Fact]
    public async Task DateFormat_CustomValidation_FailsForInvalidFormat()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DateFormat = "invalid{format}";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DateFormat));
        errors.Should().Contain("Invalid date format string.");
    }

    [Fact]
    public async Task DateFormat_CustomValidation_PassesForValidFormat()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DateFormat = "dd/MM/yyyy HH:mm";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat));

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DateFormat));
        errors.Should().BeEmpty();
    }

    #endregion

    #region Async Validation Tests

    [Fact]
    public async Task DefaultExportPath_AsyncValidation_FailsForNonexistentDirectory()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DefaultExportPath = "C:\\nonexistent\\directory";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DefaultExportPath));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DefaultExportPath));
        errors.Should().Contain("Export directory does not exist.");
    }

    [Fact]
    public async Task DefaultExportPath_AsyncValidation_PassesForExistingDirectory()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DefaultExportPath = _tempDirectory;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DefaultExportPath));

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DefaultExportPath));
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task DefaultExportPath_AsyncValidation_PassesForEmptyPath()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.DefaultExportPath = "";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DefaultExportPath));

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DefaultExportPath));
        errors.Should().BeEmpty(); // Empty path is optional
    }

    #endregion

    #region Command Validation Tests

    [Fact]
    public async Task SaveCommand_DisabledWhenValidationErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = ""; // This will cause validation error
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));

        // Act
        var canExecute = await viewModel.SaveCommand.CanExecute.FirstAsync();

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_EnabledWhenNoValidationErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = _tempLogFile; // Valid path
        viewModel.DateFormat = "yyyy-MM-dd"; // Valid format
        await Task.WhenAll(
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat))
        );
        
        // Make a change to enable the command
        viewModel.Activator.Activate(); // Activate to enable change tracking
        viewModel.UpdateInterval = 2000;
        _testScheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        // Act
        var canExecute = await viewModel.SaveCommand.CanExecute.FirstAsync();

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAllCommand_ValidatesAllProperties()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = "";
        viewModel.UpdateInterval = 50;
        viewModel.DateFormat = "";

        // Act
        var result = await viewModel.ValidateAllCommand.Execute();

        // Assert
        result.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DateFormat)).Should().NotBeEmpty();
    }

    #endregion

    #region Property Change Validation Tests

    [Fact]
    public async Task PropertyChange_TriggersValidation()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.LogFilePath = "";
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PropertyChange_ClearsValidationErrorsWhenFixed()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = ""; // Create error
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));;
        
        viewModel.HasErrors.Should().BeTrue();

        // Act
        viewModel.LogFilePath = _tempLogFile; // Fix error
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));;

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().BeEmpty();
    }

    #endregion

    #region Settings Load and Save Tests

    [Fact]
    public async Task LoadCurrentSettings_ClearsValidationErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = ""; // Create validation error
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));;
        
        viewModel.HasErrors.Should().BeTrue();

        // Act - simulate settings reload
        // Need to activate the viewmodel to enable change tracking
        viewModel.Activator.Activate();
        _settingsChangedSubject.OnNext(_defaultSettings);
        _testScheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        // Assert
        viewModel.HasErrors.Should().BeFalse();
        viewModel.LogFilePath.Should().Be(_defaultSettings.LogFilePath);
    }

    [Fact]
    public async Task SaveSettings_ValidatesBeforeSaving()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = ""; // Invalid
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));
        
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        // The save command should be disabled due to validation errors
        var canExecute = await viewModel.SaveCommand.CanExecute.FirstAsync();
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSettings_SucceedsWithValidData()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.LogFilePath = _tempLogFile;
        
        // Activate to enable change tracking
        viewModel.Activator.Activate();
        viewModel.UpdateInterval = 2000; // Make a change
        _testScheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);
        
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval));
        
        _mockSettingsService.Setup(x => x.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(Task.CompletedTask);

        // Act
        await viewModel.SaveCommand.Execute();

        // Assert
        _mockSettingsService.Verify(x => x.SaveSettingsAsync(It.Is<AppSettings>(s =>
            s.LogFilePath == _tempLogFile &&
            s.UpdateInterval == 2000)), Times.Once);
        
        viewModel.HasChanges.Should().BeFalse();
    }

    #endregion

    #region Multiple Error Scenarios

    [Fact]
    public async Task MultipleProperties_CanHaveSimultaneousErrors()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act - Set multiple invalid values
        viewModel.LogFilePath = "";
        viewModel.UpdateInterval = 50;
        viewModel.MaxLogEntries = 500;
        viewModel.DateFormat = "";
        
        await Task.WhenAll(
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.MaxLogEntries)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat))
        );

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.MaxLogEntries)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.DateFormat)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task AllErrors_ClearedWhenPropertiesFixed()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Create multiple errors
        viewModel.LogFilePath = "";
        viewModel.UpdateInterval = 50;
        viewModel.DateFormat = "";
        await Task.WhenAll(
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat))
        );
        
        viewModel.HasErrors.Should().BeTrue();

        // Act - Fix all errors
        viewModel.LogFilePath = _tempLogFile;
        viewModel.UpdateInterval = 1000;
        viewModel.DateFormat = "yyyy-MM-dd";
        await Task.WhenAll(
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.DateFormat))
        );

        // Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Validation_HandlesNullValues()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.LogFilePath = null!;
        await viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.LogFilePath));;

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.LogFilePath));
        errors.Should().Contain("Log file path is required.");
    }

    [Fact]
    public async Task Validation_HandlesExtremeValues()
    {
        // Arrange
        var viewModel = CreateViewModel();
        
        // Act
        viewModel.UpdateInterval = int.MaxValue;
        viewModel.MaxLogEntries = int.MaxValue;
        await Task.WhenAll(
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.UpdateInterval)),
            viewModel.ForceValidatePropertyAsync(nameof(SettingsViewModelValidated.MaxLogEntries))
        );

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        
        var intervalErrors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.UpdateInterval));
        intervalErrors.Should().Contain("Update interval must be between 100 and 10000 milliseconds.");
        
        var entriesErrors = viewModel.GetErrorsForProperty(nameof(SettingsViewModelValidated.MaxLogEntries));
        entriesErrors.Should().Contain("Max log entries must be between 1000 and 100000.");
    }

    #endregion
}