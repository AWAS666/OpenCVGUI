using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Draw;

/// <summary>
/// Layer that draws a circle on the image
/// </summary>
public class CircleDrawingLayer : BaseLayer
{
    [Parameter(DisplayName = "Center X", Description = "X coordinate of circle center", Order = 0)]
    [ParameterRange(0, 10000, 1)]
    public int CenterX { get; set; } = 50;

    [Parameter(DisplayName = "Center Y", Description = "Y coordinate of circle center", Order = 1)]
    [ParameterRange(0, 10000, 1)]
    public int CenterY { get; set; } = 50;

    [Parameter(DisplayName = "Radius", Description = "Radius of the circle", Order = 2)]
    [ParameterRange(1, 5000, 1)]
    public int Radius { get; set; } = 50;

    [Parameter(DisplayName = "Color", Description = "Color of the circle in hex format", Order = 3)]
    [ParameterColor]
    public string ColorHex { get; set; } = "#0000FF"; // Blue

    [Parameter(DisplayName = "Thickness", Description = "Thickness of circle border (-1 for filled)", Order = 4)]
    [ParameterRange(-1, 50, 1)]
    public int Thickness { get; set; } = 2;

    public override string GetLayerType() => "CircleDrawing";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Prepare output for drawing
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result = PrepareDrawTarget(drawTarget);

        var (b, g, r) = ColorUtils.HexToBgr(ColorHex);
        var circleColor = new Scalar(b, g, r);
        
        // Clamp center to image bounds
        int centerX = Math.Max(0, Math.Min(CenterX, result.Width - 1));
        int centerY = Math.Max(0, Math.Min(CenterY, result.Height - 1));
        
        // Ensure radius doesn't exceed image bounds
        int maxRadiusX = Math.Min(Radius, Math.Max(centerX, result.Width - centerX));
        int maxRadiusY = Math.Min(Radius, Math.Max(centerY, result.Height - centerY));
        int radius = Math.Min(maxRadiusX, maxRadiusY);
        radius = Math.Max(1, radius);
        
        var center = new Point(centerX, centerY);
        Cv2.Circle(result, center, radius, circleColor, Thickness);

        return result;
    }
}

