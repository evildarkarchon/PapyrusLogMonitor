using Avalonia.Controls;
using Avalonia.ReactiveUI;
using PapyrusMonitor.Avalonia.ViewModels;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
