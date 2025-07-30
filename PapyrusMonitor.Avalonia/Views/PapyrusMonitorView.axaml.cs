using Avalonia.ReactiveUI;
using PapyrusMonitor.Avalonia.ViewModels;

namespace PapyrusMonitor.Avalonia.Views;

public partial class PapyrusMonitorView : ReactiveUserControl<PapyrusMonitorViewModel>
{
    public PapyrusMonitorView()
    {
        InitializeComponent();
    }
}
