using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Draw;

/// <summary>
/// Layer that draws a rectangle on the image
/// </summary>
public class RectangleDrawingLayer : BaseLayer
{
    [Parameter(DisplayName = "X", Description = "X coordinate of rectangle top-left corner", Order = 0)]
    [ParameterRange(0, 10000, 1)]
    public int X { get; set; } = 10;

    [Parameter(DisplayName = "Y", Description = "Y coordinate of rectangle top-left corner", Order = 1)]
    [ParameterRange(0, 10000, 1)]
    public int Y { get; set; } = 10;

    [Parameter(DisplayName = "Width", Description = "Width of the rectangle", Order = 2)]
    [ParameterRange(1, 10000, 1)]
    public int Width { get; set; } = 100;

    [Parameter(DisplayName = "Height", Description = "Height of the rectangle", Order = 3)]
    [ParameterRange(1, 10000, 1)]
    public int Height { get; set; } = 100;

    [Parameter(DisplayName = "Color", Description = "Color of the rectangle in hex format", Order = 4)]
    [ParameterColor]
    public string ColorHex { get; set; } = "#00FF00"; // Green

    [Parameter(DisplayName = "Thickness", Description = "Thickness of rectangle border (-1 for filled)", Order = 5)]
    [ParameterRange(-1, 50, 1)]
    public int Thickness { get; set; } = 2;

    public override string GetLayerType() => "RectangleDrawing";

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
        var rectColor = new Scalar(b, g, r);
        
        // Clamp rectangle to image bounds
        int x = Math.Max(0, Math.Min(X, result.Width - 1));
        int y = Math.Max(0, Math.Min(Y, result.Height - 1));
        int width = Math.Max(1, Math.Min(Width, result.Width - x));
        int height = Math.Max(1, Math.Min(Height, result.Height - y));
        
        var rect = new Rect(x, y, width, height);
        Cv2.Rectangle(result, rect, rectColor, Thickness);

        return result;
    }
}

