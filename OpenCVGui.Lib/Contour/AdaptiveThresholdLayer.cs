using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Adaptive threshold method options
/// </summary>
public enum AdaptiveThresholdMethod
{
    /// <summary>
    /// Mean of neighborhood minus constant
    /// </summary>
    Mean,

    /// <summary>
    /// Gaussian-weighted sum of neighborhood minus constant
    /// </summary>
    Gaussian
}

/// <summary>
/// Layer that performs adaptive thresholding for varying lighting conditions
/// </summary>
public class AdaptiveThresholdLayer : BaseLayer
{
    [Parameter(DisplayName = "Max Value", Description = "Maximum value assigned to pixels exceeding threshold", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int MaxValue { get; set; } = 255;

    [Parameter(DisplayName = "Adaptive Method", Description = "Adaptive thresholding method", Order = 1)]
    public AdaptiveThresholdMethod AdaptiveMethod { get; set; } = AdaptiveThresholdMethod.Mean;

    [Parameter(DisplayName = "Threshold Type", Description = "Thresholding type", Order = 2)]
    public ThresholdTypes ThresholdType { get; set; } = ThresholdTypes.Binary;

    [Parameter(DisplayName = "Block Size", Description = "Size of neighborhood area (must be odd, e.g., 3, 5, 7)", Order = 3)]
    [ParameterRange(3, 51, 2)]
    public int BlockSize { get; set; } = 11;

    [Parameter(DisplayName = "C", Description = "Constant subtracted from mean or weighted sum", Order = 4)]
    [ParameterRange(-50, 50, 1)]
    public int C { get; set; } = 2;

    public override string GetLayerType() => "AdaptiveThreshold";

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

        // Ensure block size is odd
        int blockSize = BlockSize;
        if (blockSize % 2 == 0)
        {
            blockSize += 1;
        }

        // Convert adaptive method enum to OpenCV enum
        AdaptiveThresholdTypes adaptiveType = AdaptiveMethod == AdaptiveThresholdMethod.Mean
            ? AdaptiveThresholdTypes.MeanC
            : AdaptiveThresholdTypes.GaussianC;

        // Apply adaptive threshold
        Mat result = new Mat();
        Cv2.AdaptiveThreshold(gray, result, MaxValue, adaptiveType, ThresholdType, blockSize, C);

        gray.Dispose();

        return result;
    }
}

