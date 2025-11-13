using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that performs automatic threshold selection using Otsu's method
/// </summary>
public class OtsuThresholdLayer : BaseLayer
{
    [Parameter(DisplayName = "Max Value", Description = "Maximum value assigned to pixels exceeding threshold", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int MaxValue { get; set; } = 255;

    [Parameter(DisplayName = "Threshold Type", Description = "Thresholding type", Order = 1)]
    public ThresholdTypes ThresholdType { get; set; } = ThresholdTypes.Binary;

    /// <summary>
    /// The automatically calculated threshold value (read-only)
    /// </summary>
    public double CalculatedThreshold { get; private set; }

    public override string GetLayerType() => "OtsuThreshold";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Convert to grayscale if needed
        Mat gray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = input.Clone();
        }

        // Apply Otsu threshold (automatically calculates optimal threshold)
        Mat result = new Mat();
        double threshold = Cv2.Threshold(gray, result, MaxValue, MaxValue, ThresholdType | ThresholdTypes.Otsu);
        CalculatedThreshold = threshold;

        gray.Dispose();

        return result;
    }
}

