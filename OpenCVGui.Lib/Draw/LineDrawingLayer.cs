using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Draw;

/// <summary>
/// Layer that draws a line on the image
/// </summary>
public class LineDrawingLayer : BaseLayer
{
    [Parameter(DisplayName = "Start X", Description = "X coordinate of line start point", Order = 0)]
    [ParameterRange(0, 10000, 1)]
    public int StartX { get; set; } = 0;

    [Parameter(DisplayName = "Start Y", Description = "Y coordinate of line start point", Order = 1)]
    [ParameterRange(0, 10000, 1)]
    public int StartY { get; set; } = 0;

    [Parameter(DisplayName = "End X", Description = "X coordinate of line end point", Order = 2)]
    [ParameterRange(0, 10000, 1)]
    public int EndX { get; set; } = 100;

    [Parameter(DisplayName = "End Y", Description = "Y coordinate of line end point", Order = 3)]
    [ParameterRange(0, 10000, 1)]
    public int EndY { get; set; } = 100;

    [Parameter(DisplayName = "Color", Description = "Color of the line in hex format", Order = 4)]
    [ParameterColor]
    public string ColorHex { get; set; } = "#FF0000"; // Red

    [Parameter(DisplayName = "Thickness", Description = "Thickness of the line", Order = 5)]
    [ParameterRange(1, 50, 1)]
    public int Thickness { get; set; } = 2;

    public override string GetLayerType() => "LineDrawing";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Prepare output - clone input for drawing
        Mat result;
        if (input.Channels() == 3)
        {
            result = input.Clone();
        }
        else
        {
            // Convert grayscale to BGR for colored line
            result = new Mat();
            Cv2.CvtColor(input, result, ColorConversionCodes.GRAY2BGR);
        }

        var (b, g, r) = ColorUtils.HexToBgr(ColorHex);
        var lineColor = new Scalar(b, g, r);
        var pt1 = new Point(StartX, StartY);
        var pt2 = new Point(EndX, EndY);
        
        // Clamp points to image bounds
        pt1.X = Math.Max(0, Math.Min(pt1.X, result.Width - 1));
        pt1.Y = Math.Max(0, Math.Min(pt1.Y, result.Height - 1));
        pt2.X = Math.Max(0, Math.Min(pt2.X, result.Width - 1));
        pt2.Y = Math.Max(0, Math.Min(pt2.Y, result.Height - 1));
        
        Cv2.Line(result, pt1, pt2, lineColor, Thickness);

        return result;
    }
}

