using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that detects circles using Hough circle transform
/// </summary>
public class HoughCirclesLayer : BaseLayer
{
    [Parameter(DisplayName = "Detection Method", Description = "Circle detection method", Order = 0)]
    public HoughModes DetectionMethod { get; set; } = HoughModes.Gradient;

    [Parameter(DisplayName = "Inverse Ratio", Description = "Inverse ratio of accumulator resolution to image resolution", Order = 1)]
    [ParameterRange(1.0, 10.0, 0.1)]
    public double InverseRatio { get; set; } = 1.0;

    [Parameter(DisplayName = "Min Distance", Description = "Minimum distance between circle centers", Order = 2)]
    [ParameterRange(1, 500, 1)]
    public int MinDistance { get; set; } = 20;

    [Parameter(DisplayName = "Param1", Description = "Upper threshold for edge detection (or accumulator threshold for HOUGH_GRADIENT_ALT)", Order = 3)]
    [ParameterRange(1, 500, 1)]
    public int Param1 { get; set; } = 100;

    [Parameter(DisplayName = "Param2", Description = "Accumulator threshold for center detection (lower = more false circles)", Order = 4)]
    [ParameterRange(1, 200, 1)]
    public int Param2 { get; set; } = 30;

    [Parameter(DisplayName = "Min Radius", Description = "Minimum circle radius to detect (0 = no limit)", Order = 5)]
    [ParameterRange(0, 1000, 1)]
    public int MinRadius { get; set; } = 0;

    [Parameter(DisplayName = "Max Radius", Description = "Maximum circle radius to detect (0 = no limit)", Order = 6)]
    [ParameterRange(0, 1000, 1)]
    public int MaxRadius { get; set; } = 0;

    [Parameter(DisplayName = "Pre-blur", Description = "Apply Gaussian blur before detection", Order = 7)]
    public bool PreBlur { get; set; } = true;

    [Parameter(DisplayName = "Blur Size", Description = "Gaussian blur kernel size (must be odd)", Order = 8)]
    [ParameterRange(3, 51, 2)]
    public int BlurSize { get; set; } = 5;

    [Parameter(DisplayName = "Blur Sigma", Description = "Gaussian blur sigma value", Order = 9)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double BlurSigma { get; set; } = 1.0;

    [Parameter(DisplayName = "Draw Circles", Description = "Draw detected circles on output", Order = 10)]
    public bool DrawCircles { get; set; } = true;

    [Parameter(DisplayName = "Circle Color", Description = "Color of drawn circles in hex format", Order = 11)]
    [ParameterColor]
    public string CircleColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of circle lines (-1 = filled)", Order = 12)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Number of circles detected in the last render
    /// </summary>
    public int CircleCount { get; private set; }

    public override string GetLayerType() => "HoughCircles";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            CircleCount = 0;
            return new Mat();
        }

        // Convert to grayscale
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

        // Detect circles
        CircleSegment[] circles = Cv2.HoughCircles(
            processed,
            DetectionMethod,
            InverseRatio,
            MinDistance,
            Param1,
            Param2,
            MinRadius,
            MaxRadius
        );

        CircleCount = circles.Length;

        // Prepare output
        Mat result;
        if (DrawCircles)
        {
            Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
            result = PrepareDrawTarget(drawTarget);

            // Draw circles
            var (b, g, r) = ColorUtils.HexToBgr(CircleColorHex);
            var circleColor = new Scalar(b, g, r);

            foreach (var circle in circles)
            {
                var center = new Point((int)circle.Center.X, (int)circle.Center.Y);
                int radius = (int)circle.Radius;
                Cv2.Circle(result, center, radius, circleColor, LineThickness);
            }
        }
        else
        {
            // Return grayscale result
            result = processed.Clone();
        }

        // Cleanup
        gray.Dispose();
        blurred?.Dispose();

        return result;
    }
}

