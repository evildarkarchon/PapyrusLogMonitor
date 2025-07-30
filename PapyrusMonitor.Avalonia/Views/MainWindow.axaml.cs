using Avalonia.ReactiveUI;
using PapyrusMonitor.Avalonia.ViewModels;
using ReactiveUI;

namespace PapyrusMonitor.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            // Provide storage provider to view model when window is activated
            if (ViewModel != null)
            {
                var storageProvider = StorageProvider;
                // Update the view model with storage provider
                // This is handled through DI now
            }
        });
    }
}
