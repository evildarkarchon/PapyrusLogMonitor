using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using PapyrusMonitor.Avalonia.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels.Validation;

/// <summary>
/// Comprehensive tests for ValidationViewModelBase covering basic property validation,
/// multiple validation errors, cross-property validation, and async validation scenarios.
/// </summary>
public class ValidationViewModelBaseTests
{
    private readonly TestScheduler _testScheduler;

    public ValidationViewModelBaseTests()
    {
        _testScheduler = new TestScheduler();
    }

    /// <summary>
    /// Test ViewModel that inherits from ValidationViewModelBase for testing purposes
    /// </summary>
    private class TestValidationViewModel : ValidationViewModelBase
    {
        [Reactive] public string TestProperty { get; set; } = string.Empty;
        [Reactive] public int TestNumber { get; set; }
        [Reactive] public string TestEmail { get; set; } = string.Empty;
        [Reactive] public string TestPassword { get; set; } = string.Empty;
        [Reactive] public string TestConfirmPassword { get; set; } = string.Empty;
        [Reactive] public string TestAsyncProperty { get; set; } = string.Empty;

        public void SetupBasicValidation()
        {
            RegisterPropertyValidation(nameof(TestProperty),
                new RequiredAttribute { ErrorMessage = "TestProperty is required" },
                new MinLengthAttribute(3) { ErrorMessage = "TestProperty must be at least 3 characters" });
        }

        public void SetupMultipleValidation()
        {
            RegisterPropertyValidation(nameof(TestProperty),
                new RequiredAttribute { ErrorMessage = "Field is required" },
                new MinLengthAttribute(5) { ErrorMessage = "Must be at least 5 characters" },
                new MaxLengthAttribute(10) { ErrorMessage = "Must be no more than 10 characters" });
        }

        public void SetupEmailValidation()
        {
            RegisterPropertyValidation(nameof(TestEmail),
                new RequiredAttribute { ErrorMessage = "Email is required" },
                new EmailAddressAttribute { ErrorMessage = "Invalid email format" });
        }

        public void SetupRangeValidation()
        {
            RegisterPropertyValidation(nameof(TestNumber),
                new RangeAttribute(1, 100) { ErrorMessage = "Number must be between 1 and 100" });
        }

        public void SetupAsyncValidation()
        {
            RegisterAsyncPropertyValidation(nameof(TestAsyncProperty), ValidateAsyncProperty);
        }

        public void SetupCrossPropertyValidation()
        {
            RegisterAsyncPropertyValidation(nameof(TestConfirmPassword), ValidatePasswordConfirmation);
        }

        private async Task<IEnumerable<string>> ValidateAsyncProperty(object? value)
        {
            await Task.Delay(100); // Simulate async operation
            
            var stringValue = value as string;
            var errors = new List<string>();

            if (string.IsNullOrEmpty(stringValue))
                return errors;

            if (stringValue.Contains("invalid"))
                errors.Add("Value cannot contain 'invalid'");

            if (stringValue.Length > 20)
                errors.Add("Value is too long for async validation");

            return errors;
        }

        private async Task<IEnumerable<string>> ValidatePasswordConfirmation(object? value)
        {
            await Task.Delay(50); // Simulate async operation
            
            var errors = new List<string>();
            var confirmPassword = value as string;

            if (!string.IsNullOrEmpty(confirmPassword) && confirmPassword != TestPassword)
            {
                errors.Add("Passwords do not match");
            }

            return errors;
        }
    }

    #region Basic Property Validation Tests

    [Fact]
    public void HasErrors_InitiallyFalse()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();

        // Act & Assert
        viewModel.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void GetErrors_ReturnsEmptyForNonExistentProperty()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();

        // Act
        var errors = viewModel.GetErrors("NonExistentProperty");

        // Assert
        errors.Should().NotBeNull();
        errors.Cast<string>().Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProperty_RequiredAttribute_ReturnsErrorForEmptyValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupBasicValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().Contain("TestProperty is required");
    }

    [Fact]
    public async Task ValidateProperty_RequiredAttribute_ReturnsValidForNonEmptyValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupBasicValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "ValidValue");

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateProperty_MinLengthAttribute_ReturnsErrorForShortValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupBasicValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "ab");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().Contain("TestProperty must be at least 3 characters");
    }

    [Fact]
    public async Task ValidateProperty_EmailAttribute_ReturnsErrorForInvalidEmail()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupEmailValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestEmail), "invalid-email");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestEmail));
        errors.Should().Contain("Invalid email format");
    }

    [Fact]
    public async Task ValidateProperty_EmailAttribute_ReturnsValidForValidEmail()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupEmailValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestEmail), "test@example.com");

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateProperty_RangeAttribute_ReturnsErrorForOutOfRangeValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupRangeValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestNumber), 150);

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestNumber));
        errors.Should().Contain("Number must be between 1 and 100");
    }

    [Fact]
    public async Task ValidateProperty_RangeAttribute_ReturnsValidForInRangeValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupRangeValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestNumber), 50);

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Multiple Validation Errors Tests

    [Fact]
    public async Task ValidateProperty_MultipleValidators_ReturnsAllErrors()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupMultipleValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().Contain("Field is required");
        errors.Should().HaveCount(1); // Only required error should fire for empty string
    }

    [Fact]
    public async Task ValidateProperty_MultipleValidators_ReturnsMultipleErrorsForShortValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupMultipleValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "abc");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().Contain("Must be at least 5 characters");
        errors.Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidateProperty_MultipleValidators_ReturnsErrorForTooLongValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupMultipleValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "ThisIsWayTooLong");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty));
        errors.Should().Contain("Must be no more than 10 characters");
    }

    [Fact]
    public async Task ValidateProperty_MultipleValidators_ReturnsValidForCorrectValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupMultipleValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestProperty), "Valid");

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Async Validation Tests

    [Fact]
    public async Task ValidateProperty_AsyncValidator_ReturnsErrorForInvalidValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupAsyncValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestAsyncProperty), "invalid value");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestAsyncProperty));
        errors.Should().Contain("Value cannot contain 'invalid'");
    }

    [Fact]
    public async Task ValidateProperty_AsyncValidator_ReturnsErrorForTooLongValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupAsyncValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestAsyncProperty), 
            "This is a very long string that exceeds the limit");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestAsyncProperty));
        errors.Should().Contain("Value is too long for async validation");
    }

    [Fact]
    public async Task ValidateProperty_AsyncValidator_ReturnsValidForValidValue()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupAsyncValidation();

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestAsyncProperty), "valid value");

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateProperty_AsyncValidator_HandlesExceptions()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.RegisterAsyncPropertyValidation("TestProperty", async _ =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Test exception");
        });

        // Act
        var isValid = await viewModel.ValidatePropertyAsync("TestProperty", "test");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty("TestProperty");
        errors.Should().Contain(e => e.Contains("Validation error: Test exception"));
    }

    #endregion

    #region Cross-Property Validation Tests

    [Fact]
    public async Task ValidateProperty_CrossPropertyValidation_ReturnsErrorForMismatchedPasswords()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupCrossPropertyValidation();
        viewModel.TestPassword = "password123";

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestConfirmPassword), "differentpassword");

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestConfirmPassword));
        errors.Should().Contain("Passwords do not match");
    }

    [Fact]
    public async Task ValidateProperty_CrossPropertyValidation_ReturnsValidForMatchingPasswords()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupCrossPropertyValidation();
        viewModel.TestPassword = "password123";

        // Act
        var isValid = await viewModel.ValidatePropertyAsync(nameof(TestValidationViewModel.TestConfirmPassword), "password123");

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Error Management Tests

    [Fact]
    public void AddValidationError_AddsErrorAndRaisesEvent()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        var eventRaised = false;
        viewModel.ErrorsChanged += (_, _) => eventRaised = true;

        // Act
        viewModel.AddValidationError("TestProperty", "Test error");

        // Assert
        eventRaised.Should().BeTrue();
        viewModel.HasErrors.Should().BeTrue();
        
        var errors = viewModel.GetErrorsForProperty("TestProperty");
        errors.Should().Contain("Test error");
    }

    [Fact]
    public void AddValidationError_DoesNotAddDuplicateError()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("TestProperty", "Test error");

        // Act
        viewModel.AddValidationError("TestProperty", "Test error");

        // Assert
        var errors = viewModel.GetErrorsForProperty("TestProperty");
        errors.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveValidationError_RemovesSpecificError()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("TestProperty", "Error 1");
        viewModel.AddValidationError("TestProperty", "Error 2");

        // Act
        viewModel.RemoveValidationError("TestProperty", "Error 1");

        // Assert
        var errors = viewModel.GetErrorsForProperty("TestProperty");
        errors.Should().Contain("Error 2");
        errors.Should().NotContain("Error 1");
        errors.Should().HaveCount(1);
    }

    [Fact]
    public void ClearValidationErrors_RemovesAllErrorsForProperty()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("TestProperty", "Error 1");
        viewModel.AddValidationError("TestProperty", "Error 2");

        // Act
        viewModel.ClearValidationErrors("TestProperty");

        // Assert
        var errors = viewModel.GetErrorsForProperty("TestProperty");
        errors.Should().BeEmpty();
        viewModel.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ClearAllValidationErrors_RemovesAllErrors()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("Property1", "Error 1");
        viewModel.AddValidationError("Property2", "Error 2");

        // Act
        viewModel.ClearAllValidationErrors();

        // Assert
        viewModel.HasErrors.Should().BeFalse();
        viewModel.GetErrorsForProperty("Property1").Should().BeEmpty();
        viewModel.GetErrorsForProperty("Property2").Should().BeEmpty();
    }

    #endregion

    #region ValidateAllProperties Tests

    [Fact]
    public async Task ValidateAllProperties_ValidatesAllRegisteredProperties()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupBasicValidation();
        viewModel.SetupEmailValidation();
        viewModel.SetupRangeValidation();

        // Set invalid values
        viewModel.TestProperty = "";
        viewModel.TestEmail = "invalid-email";
        viewModel.TestNumber = 150;

        // Act
        var isValid = await viewModel.ValidateAllPropertiesAsync();

        // Assert
        isValid.Should().BeFalse();
        viewModel.HasErrors.Should().BeTrue();
        
        viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestProperty)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestEmail)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(TestValidationViewModel.TestNumber)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAllProperties_ReturnsValidWhenAllPropertiesValid()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.SetupBasicValidation();
        viewModel.SetupEmailValidation();
        viewModel.SetupRangeValidation();

        // Set valid values
        viewModel.TestProperty = "ValidValue";
        viewModel.TestEmail = "test@example.com";
        viewModel.TestNumber = 50;

        // Act
        var isValid = await viewModel.ValidateAllPropertiesAsync();

        // Assert
        isValid.Should().BeTrue();
        viewModel.HasErrors.Should().BeFalse();
    }

    #endregion

    #region IEnumerable<T> GetErrors Tests

    [Fact]
    public void GetErrors_ReturnsAllErrorsWhenPropertyNameIsNull()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("Property1", "Error 1");
        viewModel.AddValidationError("Property2", "Error 2");

        // Act
        var allErrors = viewModel.GetErrors(null);

        // Assert
        var errorList = allErrors.Cast<string>().ToList();
        errorList.Should().Contain("Error 1");
        errorList.Should().Contain("Error 2");
        errorList.Should().HaveCount(2);
    }

    [Fact]
    public void GetErrors_ReturnsAllErrorsWhenPropertyNameIsEmpty()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("Property1", "Error 1");
        viewModel.AddValidationError("Property2", "Error 2");

        // Act
        var allErrors = viewModel.GetErrors("");

        // Assert
        var errorList = allErrors.Cast<string>().ToList();
        errorList.Should().Contain("Error 1");
        errorList.Should().Contain("Error 2");
        errorList.Should().HaveCount(2);
    }

    #endregion

    #region ErrorsChanged Event Tests

    [Fact]
    public void ErrorsChanged_RaisedWhenErrorsAdded()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        var eventArgs = new List<DataErrorsChangedEventArgs>();
        viewModel.ErrorsChanged += (_, args) => eventArgs.Add(args);

        // Act
        viewModel.AddValidationError("TestProperty", "Test error");

        // Assert
        eventArgs.Should().HaveCount(1);
        eventArgs[0].PropertyName.Should().Be("TestProperty");
    }

    [Fact]
    public void ErrorsChanged_RaisedWhenErrorsRemoved()
    {
        // Arrange
        var viewModel = new TestValidationViewModel();
        viewModel.AddValidationError("TestProperty", "Test error");
        
        var eventArgs = new List<DataErrorsChangedEventArgs>();
        viewModel.ErrorsChanged += (_, args) => eventArgs.Add(args);

        // Act
        viewModel.ClearValidationErrors("TestProperty");

        // Assert
        eventArgs.Should().HaveCount(1);
        eventArgs[0].PropertyName.Should().Be("TestProperty");
    }

    #endregion
}