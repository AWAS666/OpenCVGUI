using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that applies blur filters to an image
/// </summary>
public class BlurLayer : BaseLayer
{
    [Parameter(DisplayName = "Blur Type", Description = "Type of blur to apply", Order = 0)]
    public BlurType BlurType { get; set; } = BlurType.Gaussian;

    [Parameter(DisplayName = "Kernel Size", Description = "Size of the blur kernel (must be odd number, e.g., 3, 5, 7)", Order = 1)]
    [ParameterRange(3, 51, 2)]
    public int KernelSize { get; set; } = 5;

    [Parameter(DisplayName = "Sigma X", Description = "Gaussian kernel standard deviation in X direction (for Gaussian blur)", Order = 2)]
    [ParameterRange(0.0, 10.0, 0.1)]
    public double SigmaX { get; set; } = 0.0;

    [Parameter(DisplayName = "Sigma Y", Description = "Gaussian kernel standard deviation in Y direction (for Gaussian blur, 0 = same as SigmaX)", Order = 3)]
    [ParameterRange(0.0, 10.0, 0.1)]
    public double SigmaY { get; set; } = 0.0;

    [Parameter(DisplayName = "Color Sigma", Description = "Filter sigma in the color space (for Bilateral blur)", Order = 4)]
    [ParameterRange(0.0, 200.0, 1.0)]
    public double ColorSigma { get; set; } = 50.0;

    [Parameter(DisplayName = "Space Sigma", Description = "Filter sigma in the coordinate space (for Bilateral blur)", Order = 5)]
    [ParameterRange(0.0, 200.0, 1.0)]
    public double SpaceSigma { get; set; } = 50.0;

    public override string GetLayerType() => "Blur";

    protected override Mat RenderInternal(Mat? input)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Ensure kernel size is odd
        int kernelSize = KernelSize;
        if (kernelSize % 2 == 0)
        {
            kernelSize += 1;
        }

        Mat result = new Mat();

        switch (BlurType)
        {
            case BlurType.Gaussian:
                double sigmaX = SigmaX;
                double sigmaY = SigmaY;
                if (sigmaX == 0.0)
                {
                    // Auto-calculate sigma based on kernel size
                    sigmaX = 0.3 * ((kernelSize - 1) * 0.5 - 1) + 0.8;
                }
                if (sigmaY == 0.0)
                {
                    sigmaY = sigmaX;
                }
                Cv2.GaussianBlur(input, result, new Size(kernelSize, kernelSize), sigmaX, sigmaY);
                break;

            case BlurType.Median:
                Cv2.MedianBlur(input, result, kernelSize);
                break;

            case BlurType.Bilateral:
                Cv2.BilateralFilter(input, result, kernelSize, ColorSigma, SpaceSigma);
                break;

            case BlurType.Box:
                Cv2.Blur(input, result, new Size(kernelSize, kernelSize));
                break;

            default:
                Cv2.GaussianBlur(input, result, new Size(kernelSize, kernelSize), 0, 0);
                break;
        }

        return result;
    }
}

/// <summary>
/// Types of blur filters available
/// </summary>
public enum BlurType
{
    /// <summary>
    /// Gaussian blur - smooths image using Gaussian kernel
    /// </summary>
    Gaussian,

    /// <summary>
    /// Median blur - removes noise while preserving edges
    /// </summary>
    Median,

    /// <summary>
    /// Bilateral blur - reduces noise while keeping edges sharp
    /// </summary>
    Bilateral,

    /// <summary>
    /// Box blur - simple averaging blur
    /// </summary>
    Box
}

