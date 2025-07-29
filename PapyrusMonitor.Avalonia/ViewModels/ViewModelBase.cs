using System.Reactive.Disposables;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; }

    protected ViewModelBase()
    {
        Activator = new ViewModelActivator();
        
        this.WhenActivated(disposables =>
        {
            HandleActivation(disposables);
        });
    }

    protected virtual void HandleActivation(CompositeDisposable disposables)
    {
    }
}
