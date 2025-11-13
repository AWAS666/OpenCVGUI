using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using System;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that crops an image to a specified region
/// </summary>
public class CropLayer : BaseLayer
{
    [Parameter(DisplayName = "X", Description = "Left edge of crop region in pixels", Order = 0)]
    [ParameterRange(0, 10000, 1)]
    public int X { get; set; } = 0;

    [Parameter(DisplayName = "Y", Description = "Top edge of crop region in pixels", Order = 1)]
    [ParameterRange(0, 10000, 1)]
    public int Y { get; set; } = 0;

    [Parameter(DisplayName = "Width", Description = "Width of crop region in pixels (0 = use remaining width)", Order = 2)]
    [ParameterRange(0, 10000, 1)]
    public int Width { get; set; } = 0;

    [Parameter(DisplayName = "Height", Description = "Height of crop region in pixels (0 = use remaining height)", Order = 3)]
    [ParameterRange(0, 10000, 1)]
    public int Height { get; set; } = 0;

    [Parameter(DisplayName = "Use Percentages", Description = "If true, X/Y/Width/Height are interpreted as percentages (0-100)", Order = 4)]
    public bool UsePercentages { get; set; } = false;

    public override string GetLayerType() => "Crop";

    protected override Mat RenderInternal(Mat? input)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        int x, y, width, height;

        if (UsePercentages)
        {
            // Convert percentages to pixels
            x = (int)(input.Width * (X / 100.0));
            y = (int)(input.Height * (Y / 100.0));
            width = Width > 0 ? (int)(input.Width * (Width / 100.0)) : input.Width - x;
            height = Height > 0 ? (int)(input.Height * (Height / 100.0)) : input.Height - y;
        }
        else
        {
            x = X;
            y = Y;
            width = Width > 0 ? Width : input.Width - x;
            height = Height > 0 ? Height : input.Height - y;
        }

        // Clamp to image bounds
        x = Math.Max(0, Math.Min(x, input.Width - 1));
        y = Math.Max(0, Math.Min(y, input.Height - 1));
        width = Math.Max(1, Math.Min(width, input.Width - x));
        height = Math.Max(1, Math.Min(height, input.Height - y));

        // Create ROI (Region of Interest)
        var roi = new Rect(x, y, width, height);
        var cropped = new Mat(input, roi);
        var result = cropped.Clone();
        
        cropped.Dispose();
        
        return result;
    }
}

