using Avalonia.ReactiveUI;
using PapyrusMonitor.Avalonia.ViewModels;

namespace PapyrusMonitor.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
