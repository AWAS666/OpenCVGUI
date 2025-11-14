using Avalonia.Controls;
using OpenCVGui.ViewModels;
using OpenCVGui.ViewModels.Custom;

namespace OpenCVGui.Views;

public partial class CustomControlWindow : Window
{
    public CustomControlWindow()
    {
        InitializeComponent();
        this.DataContextChanged += CustomControlWindow_DataContextChanged;
        this.Loaded += CustomControlWindow_Loaded;
    }

    private void CustomControlWindow_DataContextChanged(object? sender, System.EventArgs e)
    {
        SetupCustomControl();
    }

    private void CustomControlWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Try again after the window is loaded, in case the ContentControl content wasn't ready yet
        SetupCustomControl();
    }

    private void SetupCustomControl()
    {
        if (DataContext is not LayerViewModel layerViewModel)
            return;

        // Find MainWindowViewModel from the main window
        var windows = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        if (windows?.MainWindow?.DataContext is not MainWindowViewModel mainViewModel)
            return;

        // Wait a bit for the ContentControl to create its content, then check
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var contentControl = this.FindControl<ContentControl>("CustomControlContent");
            if (contentControl?.Content is Avalonia.Controls.UserControl customControl &&
                customControl.DataContext is PatternLearningControlViewModel patternViewModel)
            {
                // Set file dialog service from main window
                patternViewModel.SetFileDialogService(mainViewModel.GetFileDialogService());
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }
}



