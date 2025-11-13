using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Gradient method options for vertical edge detection
/// </summary>
public enum GradientMethod
{
    /// <summary>
    /// Sobel filter - standard gradient operator
    /// </summary>
    Sobel,
    
    /// <summary>
    /// Scharr filter - more sensitive to soft edges
    /// </summary>
    Scharr
}

/// <summary>
/// Output mode options for vertical edge detection
/// </summary>
public enum VerticalEdgeOutputMode
{
    /// <summary>
    /// Binary output - white edges on black background (for chain processing)
    /// </summary>
    Binary,
    
    /// <summary>
    /// Overlay - edges drawn on original image (for UI visualization)
    /// </summary>
    Overlay,
    
    /// <summary>
    /// Gradient magnitude - visualize gradient strength as grayscale
    /// </summary>
    GradientMagnitude
}

/// <summary>
/// Layer that detects soft vertical edges using vertical gradient detection
/// </summary>
public class VerticalEdgeDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Gradient Method", Description = "Method for calculating vertical gradient (Scharr is more sensitive for soft edges)", Order = 0)]
    public GradientMethod Method { get; set; } = GradientMethod.Scharr;

    [Parameter(DisplayName = "Kernel Size", Description = "Kernel size for Sobel/Scharr filter (typically 3)", Order = 1)]
    [ParameterRange(3, 7, 2)]
    public int KernelSize { get; set; } = 3;

    [Parameter(DisplayName = "Gradient Threshold", Description = "Minimum gradient magnitude to consider as edge (0-255)", Order = 2)]
    [ParameterRange(0, 255, 1)]
    public int GradientThreshold { get; set; } = 30;

    [Parameter(DisplayName = "Pre-blur Size X", Description = "Gaussian blur width before detection (for noise reduction)", Order = 3)]
    [ParameterRange(1, 100, 1)]
    public int PreBlurSizeX { get; set; } = 5;

    [Parameter(DisplayName = "Pre-blur Size Y", Description = "Gaussian blur height before detection (for noise reduction)", Order = 4)]
    [ParameterRange(1, 100, 1)]
    public int PreBlurSizeY { get; set; } = 5;

    [Parameter(DisplayName = "Pre-blur Sigma", Description = "Gaussian blur sigma value", Order = 5)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double PreBlurSigma { get; set; } = 1.0;

    [Parameter(DisplayName = "Output Mode", Description = "Output format: Binary (edges on black), Overlay (on original), or GradientMagnitude (grayscale visualization)", Order = 6)]
    public VerticalEdgeOutputMode OutputMode { get; set; } = VerticalEdgeOutputMode.Binary;

    [Parameter(DisplayName = "Edge Color", Description = "Color of edges in overlay mode (hex format)", Order = 7)]
    [ParameterColor]
    public string EdgeColorHex { get; set; } = "#FF0000"; // Red

    [Parameter(DisplayName = "Invert Output", Description = "Invert binary output (black edges on white background)", Order = 8)]
    public bool InvertOutput { get; set; } = false;

    public override string GetLayerType() => "VerticalEdgeDetection";

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

        // Apply optional pre-blur for noise reduction
        Mat processed = gray;
        Mat? blurred = null;
        if (PreBlurSizeX > 1 || PreBlurSizeY > 1)
        {
            blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new Size(PreBlurSizeX, PreBlurSizeY), PreBlurSigma);
            processed = blurred;
        }

        // Calculate vertical gradient (dx=0, dy=1)
        Mat gradient = new Mat();
        if (Method == GradientMethod.Sobel)
        {
            Cv2.Sobel(processed, gradient, MatType.CV_32F, 0, 1, KernelSize);
        }
        else // Scharr
        {
            Cv2.Scharr(processed, gradient, MatType.CV_32F, 0, 1);
        }

        // Take absolute value of gradient and convert to 8-bit (ConvertScaleAbs handles both)
        Mat normalized = new Mat();
        Cv2.ConvertScaleAbs(gradient, normalized);

        // Apply threshold
        Mat thresholded = new Mat();
        Cv2.Threshold(normalized, thresholded, GradientThreshold, 255, ThresholdTypes.Binary);

        // Invert if requested
        Mat finalEdges = thresholded;
        Mat? invertedEdges = null;
        if (InvertOutput)
        {
            invertedEdges = new Mat();
            Cv2.BitwiseNot(thresholded, invertedEdges);
            finalEdges = invertedEdges;
        }

        // Prepare output based on mode
        Mat result;
        if (OutputMode == VerticalEdgeOutputMode.Binary)
        {
            result = finalEdges.Clone();
        }
        else if (OutputMode == VerticalEdgeOutputMode.Overlay)
        {
            // Clone original for overlay
            if (input.Channels() == 3)
            {
                result = input.Clone();
            }
            else
            {
                result = new Mat();
                Cv2.CvtColor(input, result, ColorConversionCodes.GRAY2BGR);
            }

            // Draw edges on original using mask
            var (b, g, r) = ColorUtils.HexToBgr(EdgeColorHex);
            var edgeColor = new Scalar(b, g, r);
            result.SetTo(edgeColor, finalEdges);
        }
        else // GradientMagnitude
        {
            // Return normalized gradient as grayscale visualization
            result = normalized.Clone();
        }

        // Cleanup
        gray.Dispose();
        blurred?.Dispose();
        gradient.Dispose();
        normalized.Dispose();
        
        // Cleanup edge detection results
        // Note: In overlay mode, finalEdges is used as mask in SetTo, but can be disposed after
        thresholded.Dispose();
        if (invertedEdges != null)
        {
            invertedEdges.Dispose();
        }

        return result;
    }
}

