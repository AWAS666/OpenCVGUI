using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that performs Canny edge detection
/// </summary>
public class CannyEdgeDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Low Threshold", Description = "Lower threshold for edge linking", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int LowThreshold { get; set; } = 50;

    [Parameter(DisplayName = "High Threshold", Description = "Upper threshold for edge detection", Order = 1)]
    [ParameterRange(0, 255, 1)]
    public int HighThreshold { get; set; } = 150;

    [Parameter(DisplayName = "Aperture Size", Description = "Aperture size for Sobel operator (must be 3, 5, or 7)", Order = 2)]
    [ParameterRange(3, 7, 2)]
    public int ApertureSize { get; set; } = 3;

    [Parameter(DisplayName = "L2 Gradient", Description = "Use L2 gradient (more accurate but slower)", Order = 3)]
    public bool L2Gradient { get; set; } = false;

    [Parameter(DisplayName = "Pre-blur", Description = "Apply Gaussian blur before edge detection", Order = 4)]
    public bool PreBlur { get; set; } = true;

    [Parameter(DisplayName = "Blur Size", Description = "Gaussian blur kernel size (must be odd)", Order = 5)]
    [ParameterRange(3, 51, 2)]
    public int BlurSize { get; set; } = 5;

    [Parameter(DisplayName = "Blur Sigma", Description = "Gaussian blur sigma value", Order = 6)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double BlurSigma { get; set; } = 1.0;

    public override string GetLayerType() => "CannyEdgeDetection";

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

        Mat processed = gray;
        Mat? blurred = null;

        // Apply pre-blur if enabled
        if (PreBlur)
        {
            int kernelSize = BlurSize;
            if (kernelSize % 2 == 0)
            {
                kernelSize += 1;
            }
            blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(kernelSize, kernelSize), BlurSigma);
            processed = blurred;
        }

        // Ensure aperture size is valid (3, 5, or 7)
        int apertureSize = ApertureSize;
        if (apertureSize != 3 && apertureSize != 5 && apertureSize != 7)
        {
            apertureSize = 3;
        }

        // Apply Canny edge detection
        Mat result = new Mat();
        Cv2.Canny(processed, result, LowThreshold, HighThreshold, apertureSize, L2Gradient);

        // Cleanup
        gray.Dispose();
        blurred?.Dispose();

        return result;
    }
}

