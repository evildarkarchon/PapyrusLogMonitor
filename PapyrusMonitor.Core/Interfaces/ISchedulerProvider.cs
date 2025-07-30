using System.Reactive.Concurrency;

namespace PapyrusMonitor.Core.Interfaces;

public interface ISchedulerProvider
{
    IScheduler MainThread { get; }
    IScheduler TaskPool { get; }
    IScheduler CurrentThread { get; }
}
