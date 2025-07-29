using Avalonia.Controls;
using Avalonia.ReactiveUI;
using PapyrusMonitor.Avalonia.ViewModels;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.Views;

public partial class PapyrusMonitorView : ReactiveUserControl<PapyrusMonitorViewModel>
{
    public PapyrusMonitorView()
    {
        InitializeComponent();
    }
}