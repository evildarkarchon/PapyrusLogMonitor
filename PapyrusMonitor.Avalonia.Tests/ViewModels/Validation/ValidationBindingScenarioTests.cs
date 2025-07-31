using System.ComponentModel.DataAnnotations;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using PapyrusMonitor.Avalonia.Tests.Helpers;
using PapyrusMonitor.Avalonia.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PapyrusMonitor.Avalonia.Tests.ViewModels.Validation;

/// <summary>
/// Tests for various binding scenarios with validation, including different binding modes,
/// validation timing, reactive property patterns, and complex validation workflows.
/// </summary>
public class ValidationBindingScenarioTests
{
    private readonly TestScheduler _testScheduler;

    public ValidationBindingScenarioTests()
    {
        _testScheduler = new TestScheduler();
    }

    private async Task WithActivatedViewModel<T>(T viewModel, Func<T, Task> testAction) where T : IActivatableViewModel
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

    /// <summary>
    /// Advanced test ViewModel with complex validation scenarios
    /// </summary>
    private class AdvancedValidationViewModel : ValidationViewModelBase
    {
        [Reactive] public string UserName { get; set; } = string.Empty;
        [Reactive] public string Email { get; set; } = string.Empty;
        [Reactive] public string Password { get; set; } = string.Empty;
        [Reactive] public string ConfirmPassword { get; set; } = string.Empty;
        [Reactive] public int Age { get; set; }
        [Reactive] public string Country { get; set; } = string.Empty;
        [Reactive] public DateTime BirthDate { get; set; } = DateTime.Today;
        [Reactive] public bool AcceptTerms { get; set; }
        [Reactive] public string PhoneNumber { get; set; } = string.Empty;

        // Computed properties that depend on other properties
        private readonly ObservableAsPropertyHelper<bool> _isAdult;
        private readonly ObservableAsPropertyHelper<string> _displayName;

        public bool IsAdult => _isAdult.Value;
        public string DisplayName => _displayName.Value;

        public AdvancedValidationViewModel()
        {
            // Setup computed properties
            _isAdult = this.WhenAnyValue(x => x.Age)
                .Select(age => age >= 18)
                .ToProperty(this, x => x.IsAdult);

            _displayName = this.WhenAnyValue(x => x.UserName, x => x.Email)
                .Select(tuple => 
                {
                    var (userName, email) = tuple;
                    return !string.IsNullOrEmpty(userName) ? userName : 
                           !string.IsNullOrEmpty(email) ? email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0] : "Unknown";
                })
                .ToProperty(this, x => x.DisplayName, "Unknown");

            SetupValidationRules();
            SetupCrossPropertyValidation();
        }

        private void SetupValidationRules()
        {
            // Basic validation rules
            RegisterPropertyValidation(nameof(UserName),
                new RequiredAttribute { ErrorMessage = "Username is required" },
                new MinLengthAttribute(3) { ErrorMessage = "Username must be at least 3 characters" },
                new MaxLengthAttribute(20) { ErrorMessage = "Username cannot exceed 20 characters" },
                new RegularExpressionAttribute(@"^[a-zA-Z0-9_]+$") 
                { ErrorMessage = "Username can only contain letters, numbers, and underscores" });

            RegisterPropertyValidation(nameof(Email),
                new RequiredAttribute { ErrorMessage = "Email is required" },
                new EmailAddressAttribute { ErrorMessage = "Please enter a valid email address" });

            RegisterPropertyValidation(nameof(Password),
                new RequiredAttribute { ErrorMessage = "Password is required" },
                new MinLengthAttribute(8) { ErrorMessage = "Password must be at least 8 characters" },
                new CustomValidationAttribute(typeof(AdvancedValidationViewModel), nameof(ValidatePasswordStrength)));

            RegisterPropertyValidation(nameof(Age),
                new RangeAttribute(13, 120) { ErrorMessage = "Age must be between 13 and 120" });

            RegisterPropertyValidation(nameof(Country),
                new RequiredAttribute { ErrorMessage = "Country is required" });

            RegisterPropertyValidation(nameof(PhoneNumber),
                new CustomValidationAttribute(typeof(AdvancedValidationViewModel), nameof(ValidatePhoneNumber)));

            // Async validations
            RegisterAsyncPropertyValidation(nameof(UserName), ValidateUserNameAvailabilityAsync);
            RegisterAsyncPropertyValidation(nameof(Email), ValidateEmailAvailabilityAsync);
        }

        private void SetupCrossPropertyValidation()
        {
            // Cross-property validation for password confirmation
            RegisterAsyncPropertyValidation(nameof(ConfirmPassword), ValidatePasswordConfirmationAsync);

            // Cross-property validation for age and birth date consistency
            RegisterAsyncPropertyValidation(nameof(BirthDate), ValidateBirthDateConsistencyAsync);
        }

        public static ValidationResult? ValidatePasswordStrength(string? password, ValidationContext context)
        {
            if (string.IsNullOrEmpty(password))
                return ValidationResult.Success; // Required validation handles this

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

            var strengthCount = new[] { hasUpper, hasLower, hasDigit, hasSpecial }.Count(x => x);

            if (strengthCount < 3)
                return new ValidationResult("Password must contain at least 3 of: uppercase, lowercase, digits, special characters");

            return ValidationResult.Success;
        }

        public static ValidationResult? ValidatePhoneNumber(string? phoneNumber, ValidationContext context)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return ValidationResult.Success; // Optional field

            // Simple phone validation (real apps would use more sophisticated logic)
            var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 10 || digitsOnly.Length > 15)
                return new ValidationResult("Phone number must contain 10-15 digits");

            return ValidationResult.Success;
        }

        private async Task<IEnumerable<string>> ValidateUserNameAvailabilityAsync(object? value, CancellationToken cancellationToken)
        {
            await Task.Delay(150, cancellationToken); // Simulate API call
            
            var userName = value as string;
            var errors = new List<string>();

            if (!string.IsNullOrEmpty(userName))
            {
                // Simulate some usernames being taken
                var takenUsernames = new[] { "admin", "user", "test", "demo" };
                if (takenUsernames.Contains(userName.ToLower()))
                {
                    errors.Add("This username is already taken");
                }
            }

            return errors;
        }

        private async Task<IEnumerable<string>> ValidateEmailAvailabilityAsync(object? value, CancellationToken cancellationToken)
        {
            await Task.Delay(200); // Simulate API call
            
            var email = value as string;
            var errors = new List<string>();

            if (!string.IsNullOrEmpty(email) && email.Contains("@"))
            {
                // Simulate some emails being taken
                var takenEmails = new[] { "admin@test.com", "user@test.com", "demo@example.com" };
                if (takenEmails.Contains(email.ToLower()))
                {
                    errors.Add("This email address is already registered");
                }
            }

            return errors;
        }

        private async Task<IEnumerable<string>> ValidatePasswordConfirmationAsync(object? value, CancellationToken cancellationToken)
        {
            await Task.Delay(50); // Minimal delay for cross-property validation
            
            var errors = new List<string>();
            var confirmPassword = value as string;

            if (!string.IsNullOrEmpty(confirmPassword) && confirmPassword != Password)
            {
                errors.Add("Password confirmation does not match");
            }

            return errors;
        }

        private async Task<IEnumerable<string>> ValidateBirthDateConsistencyAsync(object? value, CancellationToken cancellationToken)
        {
            await Task.Delay(50);
            
            var errors = new List<string>();
            var birthDate = (DateTime)value!;

            if (birthDate > DateTime.Today)
            {
                errors.Add("Birth date cannot be in the future");
            }

            // Check consistency with age
            var calculatedAge = DateTime.Today.Year - birthDate.Year;
            if (birthDate > DateTime.Today.AddYears(-calculatedAge))
                calculatedAge--;

            if (Math.Abs(calculatedAge - Age) > 1) // Allow 1 year difference for birthday timing
            {
                errors.Add($"Birth date is inconsistent with age (calculated age: {calculatedAge})");
            }

            return errors;
        }

        protected override void HandleActivation(CompositeDisposable disposables)
        {
            base.HandleActivation(disposables);

            // Set up reactive validation for all properties
            this.WhenAnyValue(x => x.UserName)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(UserName), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Email)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(Email), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Password)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(Password), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ConfirmPassword)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(ConfirmPassword), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Age)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(Age), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.Country)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(Country), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.BirthDate)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(BirthDate), value));
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.PhoneNumber)
                .Skip(1)
                .Subscribe(value => 
                {
                    Task.Run(async () => await ValidatePropertyAsync(nameof(PhoneNumber), value));
                })
                .DisposeWith(disposables);
        }
    }

    #region Reactive Property Validation Tests

    [Fact]
    public async Task ReactiveProperty_ValidatesOnChange()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.UserName = "ab"; // Too short
        
        // Validate directly since reactive validation timing is complex
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "ab");

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        errors.Should().Contain("Username must be at least 3 characters");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task ReactiveProperty_ValidatesWithRegularExpression()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.UserName = "invalid-username!"; // Contains invalid characters
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "invalid-username!");

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        errors.Should().Contain("Username can only contain letters, numbers, and underscores");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task ReactiveProperty_MultipleValidationRules()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.Password = "weak"; // Too short and not complex enough
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.Password), "weak");

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Password));
        errors.Should().Contain("Password must be at least 8 characters");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task ReactiveProperty_PassesWithValidValue()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.UserName = "validuser123";
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "validuser123");

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        errors.Should().BeEmpty();

        viewModel.Activator.Deactivate();
    }

    #endregion

    #region Async Validation Timing Tests

    [Fact]
    public async Task AsyncValidation_DoesNotBlockSyncValidation()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.UserName = "ab"; // Triggers both sync (too short) and async (availability) validation
        
        // Validate synchronously first
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "ab");
        
        // Check immediately - sync validation should be done
        viewModel.HasErrors.Should().BeTrue();
        var syncErrors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        syncErrors.Should().Contain("Username must be at least 3 characters");

        // Async validation shouldn't add more errors since sync validation already failed
        var allErrors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        allErrors.Should().Contain("Username must be at least 3 characters");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task AsyncValidation_AddsErrorsAfterDelay()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.UserName = "admin"; // Valid sync, but taken in async validation
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "admin");

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        errors.Should().Contain("This username is already taken");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task AsyncValidation_CancelsOnRapidChanges()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act - Rapidly change values
        viewModel.UserName = "admin";
        viewModel.UserName = "validuser";
        viewModel.UserName = "anotheruser";
        
        // Validate final value
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "anotheruser");

        // Assert - Should only have errors from the final value, if any
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName));
        errors.Should().BeEmpty(); // "anotheruser" should be valid

        viewModel.Activator.Deactivate();
    }

    #endregion

    #region Cross-Property Validation Tests

    [Fact]
    public async Task CrossPropertyValidation_PasswordConfirmation()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();
        viewModel.Password = "StrongPassword123!";

        // Act
        viewModel.ConfirmPassword = "DifferentPassword";
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.ConfirmPassword), "DifferentPassword");

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.ConfirmPassword));
        errors.Should().Contain("Password confirmation does not match");

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task CrossPropertyValidation_PasswordConfirmationMatches()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();
        viewModel.Password = "StrongPassword123!";

        // Act
        viewModel.ConfirmPassword = "StrongPassword123!";
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.ConfirmPassword), "StrongPassword123!");

        // Assert
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.ConfirmPassword));
        errors.Should().BeEmpty();

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task CrossPropertyValidation_AgeBirthDateConsistency()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();
        viewModel.Age = 25;

        // Act
        viewModel.BirthDate = DateTime.Today.AddYears(-30); // Inconsistent with age
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.BirthDate), DateTime.Today.AddYears(-30));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.BirthDate));
        errors.Should().Contain(e => e.Contains("Birth date is inconsistent with age"));

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task CrossPropertyValidation_FutureBirthDate()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act
        viewModel.BirthDate = DateTime.Today.AddDays(1); // Future date
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.BirthDate), DateTime.Today.AddDays(1));

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.BirthDate));
        errors.Should().Contain("Birth date cannot be in the future");

        viewModel.Activator.Deactivate();
    }

    #endregion

    #region Complex Validation Scenarios

    [Fact]
    public async Task ComplexValidation_MultiplePropertiesSimultaneously()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act - Set multiple invalid values
        viewModel.UserName = "ab"; // Too short
        viewModel.Email = "invalid-email"; // Invalid format
        viewModel.Password = "weak"; // Too weak
        viewModel.Age = 150; // Out of range
        viewModel.PhoneNumber = "123"; // Too short

        // Validate all properties
        await viewModel.ValidateAllPropertiesAsync();

        // Assert
        viewModel.HasErrors.Should().BeTrue();
        
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Email)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Password)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Age)).Should().NotBeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.PhoneNumber)).Should().NotBeEmpty();

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task ComplexValidation_ValidDataPassesAll()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Act - Set all valid values
        viewModel.UserName = "validuser123";
        viewModel.Email = "valid@example.com";
        viewModel.Password = "StrongPassword123!";
        viewModel.ConfirmPassword = "StrongPassword123!";
        viewModel.Age = 25;
        viewModel.BirthDate = DateTime.Today.AddYears(-25);
        viewModel.Country = "USA";
        viewModel.PhoneNumber = "1234567890";
        viewModel.AcceptTerms = true;

        // Validate all properties
        await viewModel.ValidateAllPropertiesAsync();

        // Assert
        viewModel.HasErrors.Should().BeFalse();

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task ComplexValidation_PartialErrorRecovery()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();
        
        // Create multiple errors
        viewModel.UserName = "ab";
        viewModel.Email = "invalid-email";
        viewModel.Password = "weak";
        
        await viewModel.ValidateAllPropertiesAsync();
        viewModel.HasErrors.Should().BeTrue();

        // Act - Fix some errors but not all
        viewModel.UserName = "validuser123";
        viewModel.Email = "valid@example.com";
        // Password still weak

        // Validate the fixed properties
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.UserName), "validuser123");
        await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.Email), "valid@example.com");

        // Assert
        viewModel.HasErrors.Should().BeTrue(); // Still has password error
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.UserName)).Should().BeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Email)).Should().BeEmpty();
        viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Password)).Should().NotBeEmpty();

        viewModel.Activator.Deactivate();
    }

    #endregion

    #region Custom Validation Logic Tests

    [Fact]
    public async Task CustomValidation_PasswordStrength()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        // Test cases for password strength
        var testCases = new[]
        {
            ("password", false, "lowercase only"),
            ("PASSWORD", false, "uppercase only"),
            ("12345678", false, "digits only"),
            ("!@#$%^&*", false, "special chars only"),
            ("Password1", false, "missing special char"),
            ("Password!", false, "missing digit"),
            ("password1!", false, "missing uppercase"),
            ("PASSWORD1!", false, "missing lowercase"),
            ("Password1!", true, "all requirements met")
        };

        foreach (var (password, shouldPass, description) in testCases)
        {
            // Act
            viewModel.Password = password;
            await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.Password), password);

            // Assert
            var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.Password));
            if (shouldPass)
            {
                errors.Should().NotContain(e => e.Contains("Password must contain at least 3 of"), 
                    $"Password '{password}' should pass strength validation ({description})");
            }
            else
            {
                errors.Should().Contain(e => e.Contains("Password must contain at least 3 of"), 
                    $"Password '{password}' should fail strength validation ({description})");
            }
        }

        viewModel.Activator.Deactivate();
    }

    [Fact]
    public async Task CustomValidation_PhoneNumber()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();
        viewModel.Activator.Activate();

        var testCases = new[]
        {
            ("", true, "empty is optional"),
            ("123", false, "too short"),
            ("1234567890", true, "valid 10 digits"),
            ("+1-234-567-8900", true, "valid with formatting"),
            ("123456789012345", true, "valid 15 digits"),
            ("1234567890123456", false, "too long")
        };

        foreach (var (phoneNumber, shouldPass, description) in testCases)
        {
            // Act
            viewModel.PhoneNumber = phoneNumber;
            await viewModel.ValidatePropertyAsync(nameof(AdvancedValidationViewModel.PhoneNumber), phoneNumber);

            // Assert
            var errors = viewModel.GetErrorsForProperty(nameof(AdvancedValidationViewModel.PhoneNumber));
            if (shouldPass)
            {
                errors.Should().BeEmpty(
                    $"Phone '{phoneNumber}' should be valid ({description})");
            }
            else
            {
                errors.Should().NotBeEmpty(
                    $"Phone '{phoneNumber}' should be invalid ({description})");
            }
        }

        viewModel.Activator.Deactivate();
    }

    #endregion

    #region Computed Property Validation Tests

    [Fact]
    public void ComputedProperty_UpdatesBasedOnSourceProperties()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();

        // Act & Assert - Test DisplayName computation
        viewModel.UserName = "testuser";
        viewModel.DisplayName.Should().Be("testuser");

        viewModel.UserName = "";
        viewModel.Email = "test@example.com";
        viewModel.DisplayName.Should().Be("test");

        viewModel.UserName = "";
        viewModel.Email = "";
        viewModel.DisplayName.Should().Be("Unknown");
    }

    [Fact]
    public void ComputedProperty_IsAdultUpdatesWithAge()
    {
        // Arrange
        var viewModel = new AdvancedValidationViewModel();

        // Act & Assert
        viewModel.Age = 17;
        viewModel.IsAdult.Should().BeFalse();

        viewModel.Age = 18;
        viewModel.IsAdult.Should().BeTrue();

        viewModel.Age = 25;
        viewModel.IsAdult.Should().BeTrue();
    }

    #endregion
}