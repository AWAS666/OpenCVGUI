using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using System;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that matches colors in HSV space and creates a mask
/// </summary>
public class ColorMatchLayer : BaseLayer
{
    [Parameter(DisplayName = "Target Color", Description = "Target color in hex format (e.g., #FF0000 for red)", Order = 0)]
    [ParameterColor]
    public string TargetColorHex { get; set; } = "#FFFFFF";

    [Parameter(DisplayName = "Tolerance H", Description = "Hue tolerance", Order = 3)]
    [ParameterRange(0, 255, 1)]
    public int ToleranceH { get; set; } = 5;

    [Parameter(DisplayName = "Tolerance S", Description = "Saturation tolerance", Order = 4)]
    [ParameterRange(0, 255, 1)]
    public int ToleranceS { get; set; } = 50;

    [Parameter(DisplayName = "Tolerance V", Description = "Value tolerance", Order = 5)]
    [ParameterRange(0, 255, 1)]
    public int ToleranceV { get; set; } = 50;

    [Parameter(DisplayName = "Output Mode", Description = "How to output the result", Order = 6)]
    public ColorMatchOutputMode OutputMode { get; set; } = ColorMatchOutputMode.Mask;

    public override string GetLayerType() => "ColorMatch";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Convert input to HSV
        Mat hsv = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, hsv, ColorConversionCodes.BGR2HSV);
        }
        else
        {
            // If already grayscale or single channel, convert to BGR first
            Mat bgr = new Mat();
            Cv2.CvtColor(input, bgr, ColorConversionCodes.GRAY2BGR);
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);
            bgr.Dispose();
        }

        // Parse hex color to BGR
        var (b, g, r) = ColorUtils.HexToBgr(TargetColorHex);
        Scalar targetBgr = new Scalar(b, g, r);
        Mat targetMat = new Mat(1, 1, MatType.CV_8UC3, targetBgr);
        Mat targetHsvMat = new Mat();
        Cv2.CvtColor(targetMat, targetHsvMat, ColorConversionCodes.BGR2HSV);

        Vec3b targetHsv = targetHsvMat.At<Vec3b>(0, 0);

        // Calculate lower and upper bounds
        Scalar lower = new Scalar(
            Math.Max(targetHsv.Item0 - ToleranceH, 0),
            Math.Max(targetHsv.Item1 - ToleranceS, 0),
            Math.Max(targetHsv.Item2 - ToleranceV, 0)
        );

        Scalar upper = new Scalar(
            Math.Min(targetHsv.Item0 + ToleranceH, 255),
            Math.Min(targetHsv.Item1 + ToleranceS, 255),
            Math.Min(targetHsv.Item2 + ToleranceV, 255)
        );

        // Create mask
        Mat mask = new Mat();
        Cv2.InRange(hsv, lower, upper, mask);

        Mat result;
        
        // Determine effective output mode based on render mode
        ColorMatchOutputMode effectiveMode = OutputMode;
        if (renderMode == RenderMode.UI)
        {
            // For UI, prefer overlay modes for better visualization
            if (OutputMode == ColorMatchOutputMode.Mask)
            {
                effectiveMode = ColorMatchOutputMode.OriginalWithMask;
            }
        }
        else
        {
            // For chain, always use mask (actual output for next layer)
            effectiveMode = ColorMatchOutputMode.Mask;
        }

        switch (effectiveMode)
        {
            case ColorMatchOutputMode.Mask:
                result = mask.Clone();
                break;

            case ColorMatchOutputMode.Overlay:
                // Overlay mask on original image
                result = input.Clone();
                result.SetTo(new Scalar(0, 255, 0), mask); // Green overlay
                break;

            case ColorMatchOutputMode.OriginalWithMask:
                // Show original with mask as overlay
                result = input.Clone();
                Mat overlay = new Mat(result.Size(), result.Type(), new Scalar(0, 255, 0));
                overlay.CopyTo(result, mask);
                overlay.Dispose();
                break;

            default:
                result = mask.Clone();
                break;
        }

        // Cleanup
        hsv.Dispose();
        targetMat.Dispose();
        targetHsvMat.Dispose();
        mask.Dispose();

        return result;
    }
}

/// <summary>
/// Output mode for color matching
/// </summary>
public enum ColorMatchOutputMode
{
    /// <summary>
    /// Output only the mask (binary image)
    /// </summary>
    Mask,

    /// <summary>
    /// Overlay matched areas in green on original
    /// </summary>
    Overlay,

    /// <summary>
    /// Show original image with matched areas highlighted
    /// </summary>
    OriginalWithMask
}

