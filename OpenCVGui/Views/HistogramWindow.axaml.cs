using Avalonia.Controls;
using OpenCVGui.ViewModels;
using OpenCvSharp;

namespace OpenCVGui.Views;

public partial class HistogramWindow : Avalonia.Controls.Window
{
    public HistogramWindow()
    {
        InitializeComponent();
        this.Loaded += HistogramWindow_Loaded;

        if (HistogramOverlay != null)
        {
            HistogramOverlay.PointerMoved += HistogramOverlay_PointerMoved;
            HistogramOverlay.PointerExited += HistogramOverlay_PointerExited;
        }
    }

    public void UpdateHistogram(Mat image)
    {
        if (DataContext is HistogramViewModel viewModel)
        {
            viewModel.CalculateHistogram(image);
        }
    }

    private void HistogramWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is HistogramViewModel viewModel)
        {
            viewModel.RedrawHistogram();
        }
    }

    private void HistogramOverlay_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is HistogramViewModel viewModel && HistogramOverlay != null)
        {
            var position = e.GetPosition(HistogramOverlay);
            viewModel.UpdateHoverPosition(position.X, position.Y);
        }
    }

    private void HistogramOverlay_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is HistogramViewModel viewModel)
        {
            viewModel.ClearHover();
        }
    }
}
