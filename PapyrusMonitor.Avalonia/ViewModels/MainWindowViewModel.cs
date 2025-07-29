using System;
using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _title = "Papyrus Log Monitor";

    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    public PapyrusMonitorViewModel PapyrusMonitorViewModel { get; }

    public MainWindowViewModel(PapyrusMonitorViewModel papyrusMonitorViewModel)
    {
        PapyrusMonitorViewModel = papyrusMonitorViewModel ?? throw new ArgumentNullException(nameof(papyrusMonitorViewModel));
        
        ExitCommand = ReactiveCommand.Create(() =>
        {
            // Exit logic will be handled by the view
        });
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Activate the child view model
        PapyrusMonitorViewModel.Activator.Activate().DisposeWith(disposables);
        
        // Ensure cleanup when deactivated
        Disposable.Create(() =>
        {
            PapyrusMonitorViewModel?.Dispose();
        }).DisposeWith(disposables);
    }
}