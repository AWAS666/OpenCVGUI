using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that detects corners using Harris corner detection
/// </summary>
public class HarrisCornerDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Block Size", Description = "Size of neighborhood for corner detection (must be odd)", Order = 0)]
    [ParameterRange(3, 31, 2)]
    public int BlockSize { get; set; } = 2;

    [Parameter(DisplayName = "Kernel Size", Description = "Aperture parameter for Sobel operator (must be 3, 5, or 7)", Order = 1)]
    [ParameterRange(3, 7, 2)]
    public int KernelSize { get; set; } = 3;

    [Parameter(DisplayName = "K", Description = "Harris detector free parameter (typically 0.04-0.06)", Order = 2)]
    [ParameterRange(0.01, 0.2, 0.01)]
    public double K { get; set; } = 0.04;

    [Parameter(DisplayName = "Threshold", Description = "Threshold for corner response (higher = fewer corners)", Order = 3)]
    [ParameterRange(0.0, 1000.0, 1.0)]
    public double Threshold { get; set; } = 100.0;

    [Parameter(DisplayName = "Pre-blur", Description = "Apply Gaussian blur before detection", Order = 4)]
    public bool PreBlur { get; set; } = false;

    [Parameter(DisplayName = "Blur Size", Description = "Gaussian blur kernel size (must be odd)", Order = 5)]
    [ParameterRange(3, 51, 2)]
    public int BlurSize { get; set; } = 5;

    [Parameter(DisplayName = "Blur Sigma", Description = "Gaussian blur sigma value", Order = 6)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double BlurSigma { get; set; } = 1.0;

    [Parameter(DisplayName = "Draw Corners", Description = "Draw detected corners on output", Order = 7)]
    public bool DrawCorners { get; set; } = true;

    [Parameter(DisplayName = "Corner Color", Description = "Color of drawn corners in hex format", Order = 8)]
    [ParameterColor]
    public string CornerColorHex { get; set; } = "#FF0000";

    [Parameter(DisplayName = "Marker Size", Description = "Size of corner markers", Order = 9)]
    [ParameterRange(1, 20, 1)]
    public int MarkerSize { get; set; } = 5;

    /// <summary>
    /// Number of corners detected in the last render
    /// </summary>
    public int CornerCount { get; private set; }

    public override string GetLayerType() => "HarrisCornerDetection";

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

        // Convert to float for Harris corner detection
        Mat floatGray = new Mat();
        processed.ConvertTo(floatGray, MatType.CV_32F);

        // Apply Harris corner detection
        Mat cornerResponse = new Mat();
        Cv2.CornerHarris(floatGray, cornerResponse, BlockSize, KernelSize, K);

        // Normalize corner response to 0-255
        Mat normalized = new Mat();
        Cv2.Normalize(cornerResponse, normalized, 0, 255, NormTypes.MinMax, MatType.CV_8U);

        // Threshold to find corners
        Mat thresholded = new Mat();
        Cv2.Threshold(normalized, thresholded, Threshold, 255, ThresholdTypes.Binary);

        // Find corner locations
        Mat cornerPoints = new Mat();
        Cv2.FindNonZero(thresholded, cornerPoints);
        
        // Extract points from Mat
        List<Point> cornersList = new List<Point>();
        if (!cornerPoints.Empty() && cornerPoints.Rows > 0)
        {
            for (int i = 0; i < cornerPoints.Rows; i++)
            {
                Point pt = cornerPoints.Get<Point>(i, 0);
                cornersList.Add(pt);
            }
        }
        Point[] corners = cornersList.ToArray();
        CornerCount = corners.Length;

        // Prepare output
        Mat result;
        if (DrawCorners)
        {
            if (input.Channels() == 3)
            {
                result = input.Clone();
            }
            else
            {
                result = new Mat();
                Cv2.CvtColor(input, result, ColorConversionCodes.GRAY2BGR);
            }

            // Draw corners
            var (b, g, r) = ColorUtils.HexToBgr(CornerColorHex);
            var cornerColor = new Scalar(b, g, r);

            foreach (var corner in corners)
            {
                Cv2.Circle(result, corner, MarkerSize, cornerColor, -1);
            }
        }
        else
        {
            // Return thresholded result
            result = thresholded.Clone();
        }

        // Cleanup
        gray.Dispose();
        blurred?.Dispose();
        floatGray.Dispose();
        cornerResponse.Dispose();
        normalized.Dispose();
        thresholded.Dispose();
        cornerPoints.Dispose();

        return result;
    }
}

