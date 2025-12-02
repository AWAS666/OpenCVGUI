using Avalonia.Controls;
using OpenCVGui.Services;
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
        SetupCustomControl();
    }

    private void SetupCustomControl()
    {
        if (DataContext is not LayerViewModel layerViewModel)
            return;

        if (WindowService.MainWindow?.DataContext is not MainWindowViewModel mainViewModel)
            return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var contentControl = this.FindControl<ContentControl>("CustomControlContent");
            if (contentControl?.Content is Avalonia.Controls.UserControl customControl &&
                customControl.DataContext is PatternLearningControlViewModel patternViewModel)
            {
                patternViewModel.SetFileDialogService(mainViewModel.GetFileDialogService());
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }
}
