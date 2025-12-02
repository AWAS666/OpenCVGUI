using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using OpenCVGui.Services;
using System;

namespace OpenCVGui.ViewModels;

/// <summary>
/// Composable ViewModel for image viewport functionality (zoom, pan).
/// Use this as a property in other ViewModels to avoid duplicating viewport logic.
/// </summary>
public partial class ImageViewportViewModel : ViewModelBase
{
    private readonly ImageViewportService _service = new();

    [ObservableProperty]
    private Bitmap? _displayImage;

    public double ZoomLevel
    {
        get => _service.ZoomLevel;
        set
        {
            if (Math.Abs(_service.ZoomLevel - value) > 0.001)
            {
                _service.ZoomLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public double PanX
    {
        get => _service.PanX;
        set
        {
            if (Math.Abs(_service.PanX - value) > 0.001)
            {
                _service.PanX = value;
                OnPropertyChanged();
            }
        }
    }

    public double PanY
    {
        get => _service.PanY;
        set
        {
            if (Math.Abs(_service.PanY - value) > 0.001)
            {
                _service.PanY = value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        _service.ZoomIn();
        OnPropertyChanged(nameof(ZoomLevel));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _service.ZoomOut();
        OnPropertyChanged(nameof(ZoomLevel));
    }

    [RelayCommand]
    private void FitToView()
    {
        _service.FitToView();
        NotifyAllViewportPropertiesChanged();
    }

    public void ZoomAtPoint(double delta, double x, double y)
    {
        if (DisplayImage == null)
            return;

        var oldZoom = ZoomLevel;
        _service.ZoomAtPoint(delta, x, y, DisplayImage);

        if (Math.Abs(ZoomLevel - oldZoom) > 0.001)
        {
            NotifyAllViewportPropertiesChanged();
        }
    }

    public void PanImage(double deltaX, double deltaY)
    {
        _service.PanImage(deltaX, deltaY);
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    private void NotifyAllViewportPropertiesChanged()
    {
        OnPropertyChanged(nameof(ZoomLevel));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }
}

