using OpenCvSharp;
using System;

namespace OpenCVGui.ViewModels;

/// <summary>
/// Stores data for a cropped image region from the displayed image
/// </summary>
public class ImageCropData : IDisposable
{
    /// <summary>
    /// The cropped image Mat
    /// </summary>
    public Mat CroppedImage { get; set; }

    /// <summary>
    /// X coordinate of the crop region in the original image (pixels)
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the crop region in the original image (pixels)
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Width of the crop region (pixels)
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the crop region (pixels)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Timestamp when the crop was created
    /// </summary>
    public DateTime Timestamp { get; set; }

    public ImageCropData(Mat croppedImage, int x, int y, int width, int height)
    {
        CroppedImage = croppedImage ?? throw new ArgumentNullException(nameof(croppedImage));
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Timestamp = DateTime.Now;
    }

    public void Dispose()
    {
        CroppedImage?.Dispose();
    }
}


