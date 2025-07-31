using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace PapyrusMonitor.Avalonia.Tests.Helpers;

/// <summary>
/// Extension methods for reliable async testing patterns.
/// </summary>
public static class AsyncTestExtensions
{
    /// <summary>
    /// Adds a timeout to a task, throwing TimeoutException if the timeout expires.
    /// </summary>
    public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await task.WaitAsync(cts.Token);
    }

    /// <summary>
    /// Adds a timeout to a task, throwing TimeoutException if the timeout expires.
    /// </summary>
    public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        await task.WaitAsync(cts.Token);
    }

    /// <summary>
    /// Waits for a validation to complete on a specific property.
    /// </summary>
    public static async Task WaitForValidationAsync(this INotifyDataErrorInfo viewModel, 
        string propertyName, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var tcs = new TaskCompletionSource<bool>();
        
        using var cts = new CancellationTokenSource(effectiveTimeout);
        cts.Token.Register(() => tcs.TrySetException(new TimeoutException(
            $"Validation for property '{propertyName}' did not complete within {effectiveTimeout}")));

        void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
        {
            if (e.PropertyName == propertyName)
            {
                viewModel.ErrorsChanged -= OnErrorsChanged;
                tcs.TrySetResult(true);
            }
        }

        viewModel.ErrorsChanged += OnErrorsChanged;
        
        try
        {
            await tcs.Task;
        }
        finally
        {
            viewModel.ErrorsChanged -= OnErrorsChanged;
        }
    }

    /// <summary>
    /// Waits for any validation to occur.
    /// </summary>
    public static async Task WaitForAnyValidationAsync(this INotifyDataErrorInfo viewModel, 
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var tcs = new TaskCompletionSource<bool>();
        
        using var cts = new CancellationTokenSource(effectiveTimeout);
        cts.Token.Register(() => tcs.TrySetException(new TimeoutException(
            $"No validation occurred within {effectiveTimeout}")));

        void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
        {
            viewModel.ErrorsChanged -= OnErrorsChanged;
            tcs.TrySetResult(true);
        }

        viewModel.ErrorsChanged += OnErrorsChanged;
        
        try
        {
            await tcs.Task;
        }
        finally
        {
            viewModel.ErrorsChanged -= OnErrorsChanged;
        }
    }

    /// <summary>
    /// Waits for a property change notification.
    /// </summary>
    public static async Task WaitForPropertyChangeAsync(this INotifyPropertyChanged viewModel,
        string propertyName, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var tcs = new TaskCompletionSource<bool>();
        
        using var cts = new CancellationTokenSource(effectiveTimeout);
        cts.Token.Register(() => tcs.TrySetException(new TimeoutException(
            $"Property '{propertyName}' did not change within {effectiveTimeout}")));

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == propertyName)
            {
                viewModel.PropertyChanged -= OnPropertyChanged;
                tcs.TrySetResult(true);
            }
        }

        viewModel.PropertyChanged += OnPropertyChanged;
        
        try
        {
            await tcs.Task;
        }
        finally
        {
            viewModel.PropertyChanged -= OnPropertyChanged;
        }
    }

    /// <summary>
    /// Executes an action and waits for validation to complete.
    /// </summary>
    public static async Task ExecuteAndWaitForValidationAsync(
        this INotifyDataErrorInfo viewModel,
        Action action,
        string propertyName,
        TimeSpan? timeout = null)
    {
        var validationTask = viewModel.WaitForValidationAsync(propertyName, timeout);
        action();
        await validationTask;
    }

    /// <summary>
    /// Retries an async operation with exponential backoff.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        Exception lastException = new InvalidOperationException("No attempts were made");

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (i < maxAttempts - 1)
            {
                lastException = ex;
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        throw lastException;
    }

    /// <summary>
    /// Waits for a condition to become true.
    /// </summary>
    public static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var effectivePollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        
        using var cts = new CancellationTokenSource(effectiveTimeout);
        
        while (!condition())
        {
            if (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Condition was not met within {effectiveTimeout}");
            }
            
            await Task.Delay(effectivePollInterval, cts.Token);
        }
    }
}