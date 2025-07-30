using System.Reactive.Concurrency;
using FluentAssertions;
using PapyrusMonitor.Avalonia.Services;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.Tests.Services;

public class AvaloniaSchedulerProviderTests
{
    [Fact]
    public void MainThread_Should_Return_RxApp_MainThreadScheduler()
    {
        // Arrange
        var provider = new AvaloniaSchedulerProvider();
        
        // Act
        var scheduler = provider.MainThread;
        
        // Assert
        scheduler.Should().BeSameAs(RxApp.MainThreadScheduler);
    }
    
    [Fact]
    public void TaskPool_Should_Return_RxApp_TaskpoolScheduler()
    {
        // Arrange
        var provider = new AvaloniaSchedulerProvider();
        
        // Act
        var scheduler = provider.TaskPool;
        
        // Assert
        scheduler.Should().BeSameAs(RxApp.TaskpoolScheduler);
    }
    
    [Fact]
    public void CurrentThread_Should_Return_CurrentThreadScheduler_Instance()
    {
        // Arrange
        var provider = new AvaloniaSchedulerProvider();
        
        // Act
        var scheduler = provider.CurrentThread;
        
        // Assert
        scheduler.Should().BeSameAs(CurrentThreadScheduler.Instance);
    }
    
    [Fact]
    public void Should_Return_Valid_Schedulers()
    {
        // Arrange
        var provider = new AvaloniaSchedulerProvider();
        
        // Act
        var mainThread = provider.MainThread;
        var taskPool = provider.TaskPool;
        var currentThread = provider.CurrentThread;
        
        // Assert
        mainThread.Should().NotBeNull();
        taskPool.Should().NotBeNull();
        currentThread.Should().NotBeNull();
        
        // CurrentThread should always be CurrentThreadScheduler.Instance
        currentThread.Should().BeOfType<CurrentThreadScheduler>();
    }
    
    [Fact]
    public void Multiple_Calls_Should_Return_Same_Scheduler_Instances()
    {
        // Arrange
        var provider = new AvaloniaSchedulerProvider();
        
        // Act & Assert
        var mainThread1 = provider.MainThread;
        var mainThread2 = provider.MainThread;
        mainThread1.Should().BeSameAs(mainThread2);
        
        var taskPool1 = provider.TaskPool;
        var taskPool2 = provider.TaskPool;
        taskPool1.Should().BeSameAs(taskPool2);
        
        var currentThread1 = provider.CurrentThread;
        var currentThread2 = provider.CurrentThread;
        currentThread1.Should().BeSameAs(currentThread2);
    }
}