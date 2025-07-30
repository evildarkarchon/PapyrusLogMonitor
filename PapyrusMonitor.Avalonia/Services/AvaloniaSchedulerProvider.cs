using System.Reactive.Concurrency;
using PapyrusMonitor.Core.Interfaces;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.Services;

public class AvaloniaSchedulerProvider : ISchedulerProvider
{
    public IScheduler MainThread
    {
        get => RxApp.MainThreadScheduler;
    }

    public IScheduler TaskPool
    {
        get => RxApp.TaskpoolScheduler;
    }

    public IScheduler CurrentThread
    {
        get => CurrentThreadScheduler.Instance;
    }
}
