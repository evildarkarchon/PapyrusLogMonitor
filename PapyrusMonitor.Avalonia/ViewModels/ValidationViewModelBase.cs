using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

/// <summary>
/// Base ViewModel class that implements INotifyDataErrorInfo for validation support.
/// Provides comprehensive validation functionality for reactive properties.
/// </summary>
public abstract class ValidationViewModelBase : ReactiveObject, IActivatableViewModel, INotifyDataErrorInfo
{
    private readonly ConcurrentDictionary<string, List<string>> _validationErrors = new();
    private readonly ConcurrentDictionary<string, List<ValidationAttribute>> _propertyValidators = new();
    private readonly ConcurrentDictionary<string, Func<object?, CancellationToken, Task<IEnumerable<string>>>> _asyncValidators = new();
    private readonly SemaphoreSlim _validationSemaphore = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ConcurrentDictionary<string, (object? Value, DateTime Timestamp, List<string> Errors)> _validationCache = new();
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

    protected ValidationViewModelBase()
    {
        Activator = new ViewModelActivator();
        this.WhenActivated(HandleActivation);
    }

    public ViewModelActivator Activator { get; }

    /// <summary>
    /// Indicates whether the ViewModel has any validation errors.
    /// </summary>
    public bool HasErrors => _validationErrors.Any(x => x.Value.Count > 0);

    /// <summary>
    /// Event raised when validation errors change for any property.
    /// </summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>
    /// Gets validation errors for the specified property.
    /// </summary>
    /// <param name="propertyName">The property name, or null for object-level errors.</param>
    /// <returns>Collection of validation error messages.</returns>
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _validationErrors.SelectMany(x => x.Value).ToList();

        return _validationErrors.TryGetValue(propertyName, out var errors) ? errors.ToList() : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Gets validation errors for the specified property as strings.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>Collection of validation error messages as strings.</returns>
    public IEnumerable<string> GetErrorsForProperty(string propertyName)
    {
        return _validationErrors.TryGetValue(propertyName, out var errors) ? errors.ToList() : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Registers validation attributes for a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="validators">Validation attributes to apply.</param>
    protected void RegisterPropertyValidation(string propertyName, params ValidationAttribute[] validators)
    {
        _propertyValidators[propertyName] = validators.ToList();
    }

    /// <summary>
    /// Registers an async validator for a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="asyncValidator">Async validation function that returns error messages.</param>
    public void RegisterAsyncPropertyValidation(string propertyName, 
        Func<object?, CancellationToken, Task<IEnumerable<string>>> asyncValidator)
    {
        _asyncValidators[propertyName] = asyncValidator;
    }

    /// <summary>
    /// Registers an async validator for a property (legacy overload without cancellation).
    /// </summary>
    public void RegisterAsyncPropertyValidation(string propertyName, 
        Func<object?, Task<IEnumerable<string>>> asyncValidator)
    {
        _asyncValidators[propertyName] = (value, _) => asyncValidator(value);
    }

    /// <summary>
    /// Validates a specific property and updates error collection.
    /// </summary>
    /// <param name="propertyName">The property name to validate.</param>
    /// <param name="value">The property value.</param>
    /// <returns>True if validation passed, false otherwise.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "ValidationContext is used in controlled manner with known types")]
    public Task<bool> ValidatePropertyAsync(string propertyName, object? value)
    {
        return ValidatePropertyAsync(propertyName, value, CancellationToken.None);
    }

    /// <summary>
    /// Validates a specific property and updates error collection.
    /// </summary>
    /// <param name="propertyName">The property name to validate.</param>
    /// <param name="value">The property value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if validation passed, false otherwise.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "ValidationContext is used in controlled manner with known types")]
    public async Task<bool> ValidatePropertyAsync(string propertyName, object? value, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalCts.Token);
        await _validationSemaphore.WaitAsync(linkedCts.Token);
        
        try
        {
            var errors = new List<string>();

            // Run synchronous validators
            if (_propertyValidators.TryGetValue(propertyName, out var validators))
            {
                var validationContext = new ValidationContext(this) { MemberName = propertyName };
                
                foreach (var validator in validators)
                {
                    var result = validator.GetValidationResult(value, validationContext);
                    if (result != ValidationResult.Success && !string.IsNullOrEmpty(result?.ErrorMessage))
                    {
                        errors.Add(result.ErrorMessage);
                    }
                }
            }

            // Run async validators with caching
            if (_asyncValidators.TryGetValue(propertyName, out var asyncValidator))
            {
                // Check cache first
                if (_validationCache.TryGetValue(propertyName, out var cached) &&
                    Equals(cached.Value, value) &&
                    DateTime.UtcNow - cached.Timestamp < _cacheExpiration)
                {
                    errors.AddRange(cached.Errors);
                }
                else
                {
                    try
                    {
                        var asyncErrors = await asyncValidator(value, linkedCts.Token);
                        var errorList = asyncErrors.ToList();
                        errors.AddRange(errorList);
                        
                        // Cache the result
                        _validationCache[propertyName] = (value, DateTime.UtcNow, errorList);
                    }
                    catch (OperationCanceledException) when (linkedCts.Token.IsCancellationRequested)
                    {
                        // Validation was cancelled, don't treat as error
                        throw;
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException))
                    {
                        errors.Add($"Validation error: {ex.Message}");
                    }
                }
            }

            // Update error collection
            SetErrors(propertyName, errors);
            
            return errors.Count == 0;
        }
        finally
        {
            _validationSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates all properties that have registered validators.
    /// </summary>
    /// <returns>True if all validations passed, false otherwise.</returns>
    public Task<bool> ValidateAllPropertiesAsync()
    {
        return ValidateAllPropertiesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Validates all properties that have registered validators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all validations passed, false otherwise.</returns>
    public async Task<bool> ValidateAllPropertiesAsync(CancellationToken cancellationToken)
    {
        var allPropertyNames = _propertyValidators.Keys.Union(_asyncValidators.Keys).ToList();
        
        // Capture property values atomically
        var propertyValues = allPropertyNames.ToDictionary(
            name => name, 
            name => GetPropertyValue(name));
        
        // Run validations concurrently
        var validationTasks = propertyValues.Select(kvp =>
            ValidatePropertyAsync(kvp.Key, kvp.Value, cancellationToken));
        
        var results = await Task.WhenAll(validationTasks);
        return results.All(x => x);
    }

    /// <summary>
    /// Adds a validation error for a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="error">The error message.</param>
    public void AddValidationError(string propertyName, string error)
    {
        var errors = _validationErrors.GetOrAdd(propertyName, _ => new List<string>());
        
        lock (errors)
        {
            if (!errors.Contains(error))
            {
                errors.Add(error);
                OnErrorsChanged(propertyName);
            }
        }
    }

    /// <summary>
    /// Removes a specific validation error for a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="error">The error message to remove.</param>
    public void RemoveValidationError(string propertyName, string error)
    {
        if (_validationErrors.TryGetValue(propertyName, out var errors))
        {
            bool removed;
            lock (errors)
            {
                removed = errors.Remove(error);
                if (removed && errors.Count == 0)
                {
                    _validationErrors.TryRemove(propertyName, out _);
                }
            }
            
            if (removed)
            {
                OnErrorsChanged(propertyName);
            }
        }
    }

    /// <summary>
    /// Clears all validation errors for a property.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    public void ClearValidationErrors(string propertyName)
    {
        if (_validationErrors.TryRemove(propertyName, out _))
        {
            OnErrorsChanged(propertyName);
        }
    }

    /// <summary>
    /// Clears all validation errors.
    /// </summary>
    public void ClearAllValidationErrors()
    {
        var propertyNames = _validationErrors.Keys.ToList();
        _validationErrors.Clear();
        _validationCache.Clear();
        
        foreach (var propertyName in propertyNames)
        {
            OnErrorsChanged(propertyName);
        }
    }

    /// <summary>
    /// Invalidates the validation cache for a specific property.
    /// </summary>
    /// <param name="propertyName">The property name to invalidate.</param>
    public void InvalidateValidationCache(string propertyName)
    {
        _validationCache.TryRemove(propertyName, out _);
    }

    /// <summary>
    /// Invalidates the entire validation cache.
    /// </summary>
    public void InvalidateAllValidationCache()
    {
        _validationCache.Clear();
    }

    /// <summary>
    /// Sets validation errors for a property, replacing any existing errors.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="errors">The error messages.</param>
    private void SetErrors(string propertyName, IList<string> errors)
    {
        var errorList = errors.ToList();
        
        if (_validationErrors.TryGetValue(propertyName, out var existingErrors))
        {
            lock (existingErrors)
            {
                if (existingErrors.SequenceEqual(errorList))
                {
                    return; // No change, avoid update
                }
            }
        }
        
        if (errorList.Count > 0)
        {
            _validationErrors[propertyName] = errorList;
        }
        else
        {
            _validationErrors.TryRemove(propertyName, out _);
        }

        OnErrorsChanged(propertyName);
    }

    /// <summary>
    /// Raises the ErrorsChanged event.
    /// </summary>
    /// <param name="propertyName">The property name that had errors changed.</param>
    protected virtual void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        this.RaisePropertyChanged(nameof(HasErrors));
    }

    /// <summary>
    /// Gets the value of a property using reflection.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value.</returns>
    private object? GetPropertyValue(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        return property?.GetValue(this);
    }

    protected virtual void HandleActivation(CompositeDisposable disposables)
    {
        // Derived classes can override this for additional activation logic
        
        // Dispose the cancellation token source when the view model is deactivated
        Disposable.Create(() => _disposalCts.Cancel()).DisposeWith(disposables);
    }
    
    /// <summary>
    /// Disposes resources used by the validation view model.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposalCts?.Cancel();
            _disposalCts?.Dispose();
            _validationSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Disposes resources used by the validation view model.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}