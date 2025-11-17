using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that performs binary thresholding
/// </summary>
public class ThresholdLayer : BaseLayer
{
    [Parameter(DisplayName = "Threshold Value", Description = "Threshold value", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int ThresholdValue { get; set; } = 127;

    [Parameter(DisplayName = "Max Value", Description = "Maximum value assigned to pixels exceeding threshold", Order = 1)]
    [ParameterRange(0, 255, 1)]
    public int MaxValue { get; set; } = 255;

    [Parameter(DisplayName = "Threshold Type", Description = "Type of thresholding operation", Order = 2)]
    public ThresholdTypes ThresholdType { get; set; } = ThresholdTypes.Binary;

    public override string GetLayerType() => "Threshold";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat gray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = input.Clone();
        }

        Mat result = new Mat();
        Cv2.Threshold(gray, result, ThresholdValue, MaxValue, ThresholdType);

        gray.Dispose();

        return result;
    }
}

