using System.Reactive.Disposables;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    protected ViewModelBase()
    {
        Activator = new ViewModelActivator();

        this.WhenActivated(HandleActivation);
    }

    public ViewModelActivator Activator { get; }

    protected virtual void HandleActivation(CompositeDisposable disposables)
    {
    }
}
