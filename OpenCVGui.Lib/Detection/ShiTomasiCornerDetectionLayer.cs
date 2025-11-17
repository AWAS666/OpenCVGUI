using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that detects corners using Shi-Tomasi corner detection (good features to track)
/// </summary>
public class ShiTomasiCornerDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Max Corners", Description = "Maximum number of corners to detect", Order = 0)]
    [ParameterRange(1, 1000, 1)]
    public int MaxCorners { get; set; } = 100;

    [Parameter(DisplayName = "Quality Level", Description = "Minimum quality of corners (0.0-1.0, higher = better quality)", Order = 1)]
    [ParameterRange(0.01, 1.0, 0.01)]
    public double QualityLevel { get; set; } = 0.01;

    [Parameter(DisplayName = "Min Distance", Description = "Minimum distance between corners", Order = 2)]
    [ParameterRange(1, 100, 1)]
    public int MinDistance { get; set; } = 10;

    [Parameter(DisplayName = "Block Size", Description = "Size of neighborhood for corner detection (must be odd)", Order = 3)]
    [ParameterRange(3, 31, 2)]
    public int BlockSize { get; set; } = 3;

    [Parameter(DisplayName = "Use Harris Detector", Description = "Use Harris corner detector instead of Shi-Tomasi", Order = 4)]
    public bool UseHarrisDetector { get; set; } = false;

    [Parameter(DisplayName = "K", Description = "Free parameter for Harris detector (only if UseHarrisDetector is true)", Order = 5)]
    [ParameterRange(0.01, 0.2, 0.01)]
    public double K { get; set; } = 0.04;

    [Parameter(DisplayName = "Pre-blur", Description = "Apply Gaussian blur before detection", Order = 6)]
    public bool PreBlur { get; set; } = false;

    [Parameter(DisplayName = "Blur Size", Description = "Gaussian blur kernel size (must be odd)", Order = 7)]
    [ParameterRange(3, 51, 2)]
    public int BlurSize { get; set; } = 5;

    [Parameter(DisplayName = "Blur Sigma", Description = "Gaussian blur sigma value", Order = 8)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double BlurSigma { get; set; } = 1.0;

    [Parameter(DisplayName = "Draw Corners", Description = "Draw detected corners on output", Order = 9)]
    public bool DrawCorners { get; set; } = true;

    [Parameter(DisplayName = "Corner Color", Description = "Color of drawn corners in hex format", Order = 10)]
    [ParameterColor]
    public string CornerColorHex { get; set; } = "#FF0000";

    [Parameter(DisplayName = "Marker Size", Description = "Size of corner markers", Order = 11)]
    [ParameterRange(1, 20, 1)]
    public int MarkerSize { get; set; } = 5;

    /// <summary>
    /// Number of corners detected in the last render
    /// </summary>
    public int CornerCount { get; private set; }

    public override string GetLayerType() => "ShiTomasiCornerDetection";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            CornerCount = 0;
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

        // Ensure block size is odd
        int blockSize = BlockSize;
        if (blockSize % 2 == 0)
        {
            blockSize += 1;
        }

        // Detect corners using GoodFeaturesToTrack (Shi-Tomasi)
        Point2f[]? corners = Cv2.GoodFeaturesToTrack(
            processed,
            MaxCorners,
            QualityLevel,
            MinDistance,
            null,
            blockSize,
            UseHarrisDetector,
            K
        );

        CornerCount = corners?.Length ?? 0;

        // Prepare output
        Mat result;
        if (DrawCorners && corners != null && corners.Length > 0)
        {
            Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
            result = PrepareDrawTarget(drawTarget);

            // Draw corners
            var (b, g, r) = ColorUtils.HexToBgr(CornerColorHex);
            var cornerColor = new Scalar(b, g, r);

            foreach (var corner in corners)
            {
                var point = new Point((int)corner.X, (int)corner.Y);
                Cv2.Circle(result, point, MarkerSize, cornerColor, -1);
            }
        }
        else
        {
            // Return processed result
            result = processed.Clone();
        }

        // Cleanup
        gray.Dispose();
        blurred?.Dispose();

        return result;
    }
}

