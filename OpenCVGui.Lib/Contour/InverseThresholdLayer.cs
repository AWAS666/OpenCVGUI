using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that performs inverted binary thresholding
/// </summary>
public class InverseThresholdLayer : BaseLayer
{
    [Parameter(DisplayName = "Threshold Value", Description = "Threshold value", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int ThresholdValue { get; set; } = 127;

    [Parameter(DisplayName = "Max Value", Description = "Maximum value assigned to pixels exceeding threshold", Order = 1)]
    [ParameterRange(0, 255, 1)]
    public int MaxValue { get; set; } = 255;

    public override string GetLayerType() => "InverseThreshold";

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

        // Apply inverse binary threshold
        Mat result = new Mat();
        Cv2.Threshold(gray, result, ThresholdValue, MaxValue, ThresholdTypes.BinaryInv);

        gray.Dispose();

        return result;
    }
}

