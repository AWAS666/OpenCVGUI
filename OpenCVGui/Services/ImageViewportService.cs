using System;
using Avalonia.Media.Imaging;

namespace OpenCVGui.Services;

public class ImageViewportService
{
    private double _zoomLevel = 1.0;
    private double _panX = 0.0;
    private double _panY = 0.0;

    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 0.1;

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => _zoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, value));
    }

    public double PanX
    {
        get => _panX;
        set => _panX = value;
    }

    public double PanY
    {
        get => _panY;
        set => _panY = value;
    }

    public void ZoomIn()
    {
        ZoomLevel = Math.Min(MaxZoom, ZoomLevel + ZoomStep);
    }

    public void ZoomOut()
    {
        ZoomLevel = Math.Max(MinZoom, ZoomLevel - ZoomStep);
    }

    public void FitToView()
    {
        ZoomLevel = 1.0;
        PanX = 0.0;
        PanY = 0.0;
    }

    public void ZoomAtPoint(double delta, double x, double y, Bitmap? displayImage)
    {
        if (displayImage == null)
            return;

        var oldZoom = ZoomLevel;
        var zoomDelta = delta * ZoomStep * 0.5;
        var newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, ZoomLevel + zoomDelta));

        if (Math.Abs(newZoom - oldZoom) < 0.001)
            return;

        var imageWidth = displayImage.PixelSize.Width;
        var imageHeight = displayImage.PixelSize.Height;

        var imageSpaceX = x - imageWidth / 2.0;
        var imageSpaceY = y - imageHeight / 2.0;

        var zoomFactor = newZoom / oldZoom;

        PanX = imageSpaceX - (imageSpaceX - PanX) * zoomFactor;
        PanY = imageSpaceY - (imageSpaceY - PanY) * zoomFactor;

        ZoomLevel = newZoom;
    }

    public void PanImage(double deltaX, double deltaY)
    {
        PanX += deltaX;
        PanY += deltaY;
    }
}

