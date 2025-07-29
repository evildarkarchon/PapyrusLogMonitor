using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private PapyrusMonitorViewModel? _papyrusMonitor;
    private string _title = "Papyrus Log Monitor";

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public PapyrusMonitorViewModel? PapyrusMonitor
    {
        get => _papyrusMonitor;
        set => this.RaiseAndSetIfChanged(ref _papyrusMonitor, value);
    }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public MainWindowViewModel()
    {
        ExitCommand = ReactiveCommand.Create(() =>
        {
            // Exit logic will be handled by the view
        });
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // TODO: Initialize Papyrus monitoring with proper dependency injection
        // For now, PapyrusMonitor will be set externally via DI container
        
        // Ensure cleanup when deactivated
        Disposable.Create(() =>
        {
            PapyrusMonitor?.Dispose();
        }).DisposeWith(disposables);
    }
}