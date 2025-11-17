using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Color space options for channel extraction
/// </summary>
public enum ColorSpaceType
{
    /// <summary>
    /// Blue-Green-Red color space (default)
    /// </summary>
    BGR,
    
    /// <summary>
    /// Hue-Saturation-Value color space
    /// </summary>
    HSV,
    
    /// <summary>
    /// L*a*b* color space (perceptually uniform)
    /// </summary>
    LAB
}

/// <summary>
/// Layer that performs binary thresholding on a specific channel, finds boxes using connected components, and draws rotated rectangles
/// </summary>
public class BinaryThresholdLayer : BaseLayer
{
    [Parameter(DisplayName = "Inner Threshold", Description = "Threshold value for the inner box detection", Order = 0)]
    [ParameterRange(0, 255, 1)]
    public int InnerThreshold { get; set; } = 127;

    [Parameter(DisplayName = "Outer Threshold", Description = "Threshold value for the outer box detection", Order = 1)]
    [ParameterRange(0, 255, 1)]
    public int OuterThreshold { get; set; } = 50;

    [Parameter(DisplayName = "Area Min (Small Box)", Description = "Minimum area for the smaller (inner) box", Order = 2)]
    [ParameterRange(0, 100000, 1)]
    public int AreaMinSmallBox { get; set; } = 3000;

    [Parameter(DisplayName = "Blur Size X", Description = "Gaussian blur kernel width", Order = 3)]
    [ParameterRange(1, 100, 1)]
    public int BlurSizeX { get; set; } = 7;

    [Parameter(DisplayName = "Blur Size Y", Description = "Gaussian blur kernel height", Order = 4)]
    [ParameterRange(1, 100, 1)]
    public int BlurSizeY { get; set; } = 25;

    [Parameter(DisplayName = "Blur Amount (Sigma)", Description = "Gaussian blur sigma value (standard deviation). Higher values = more blur", Order = 5)]
    [ParameterRange(0.1, 50.0, 0.1)]
    public double BlurSigma { get; set; } = 1.5;

    [Parameter(DisplayName = "Color Space", Description = "Color space to use for channel extraction", Order = 6)]
    public ColorSpaceType ColorSpace { get; set; } = ColorSpaceType.BGR;

    [Parameter(DisplayName = "Channel Type", Description = "Channel to extract (BGR: 0=Blue, 1=Green, 2=Red | HSV: 0=Hue, 1=Saturation, 2=Value | LAB: 0=L, 1=A, 2=B)", Order = 7)]
    [ParameterRange(0, 2, 1)]
    public int ChannelType { get; set; } = 1;

    [Parameter(DisplayName = "Morph Kernel Size X", Description = "Width of the morphological kernel", Order = 8)]
    [ParameterRange(1, 50, 1)]
    public int MorphKernelSizeX { get; set; } = 13;

    [Parameter(DisplayName = "Morph Kernel Size Y", Description = "Height of the morphological kernel", Order = 9)]
    [ParameterRange(1, 50, 1)]
    public int MorphKernelSizeY { get; set; } = 1;

    [Parameter(DisplayName = "Inner Box Color", Description = "Color of the inner box rectangle in hex format", Order = 10)]
    [ParameterColor]
    public string InnerBoxColorHex { get; set; } = "#FF00FF"; // Magenta

    [Parameter(DisplayName = "Outer Box Color", Description = "Color of the outer box rectangle in hex format", Order = 11)]
    [ParameterColor]
    public string OuterBoxColorHex { get; set; } = "#00FF00"; // Green

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of rectangle lines (-1 = filled)", Order = 12)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    public override string GetLayerType() => "BinaryThreshold";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Determine draw target
        // In UI mode: draw on chainInput (original user input) if available, otherwise input
        // In Chain mode: draw on input (chain output)
        Mat? drawTarget = null;
        if (renderMode == RenderMode.UI && chainInput != null && !chainInput.Empty())
        {
            drawTarget = chainInput;
        }
        else
        {
            drawTarget = input;
        }

        // Prepare output for drawing - convert to BGR if needed for colored rectangles
        Mat result;
        if (drawTarget!.Channels() == 3)
        {
            result = drawTarget.Clone();
        }
        else
        {
            result = new Mat();
            Cv2.CvtColor(drawTarget, result, ColorConversionCodes.GRAY2BGR);
        }

        // Convert to target color space if needed
        Mat colorSpaceMat = new Mat();
        if (input.Channels() == 3)
        {
            switch (ColorSpace)
            {
                case ColorSpaceType.BGR:
                    colorSpaceMat = input.Clone();
                    break;
                case ColorSpaceType.HSV:
                    Cv2.CvtColor(input, colorSpaceMat, ColorConversionCodes.BGR2HSV);
                    break;
                case ColorSpaceType.LAB:
                    Cv2.CvtColor(input, colorSpaceMat, ColorConversionCodes.BGR2Lab);
                    break;
                default:
                    colorSpaceMat = input.Clone();
                    break;
            }
        }
        else
        {
            // If input is already grayscale, convert to BGR first, then to target color space
            Mat bgr = new Mat();
            Cv2.CvtColor(input, bgr, ColorConversionCodes.GRAY2BGR);
            
            switch (ColorSpace)
            {
                case ColorSpaceType.BGR:
                    colorSpaceMat = bgr;
                    break;
                case ColorSpaceType.HSV:
                    Cv2.CvtColor(bgr, colorSpaceMat, ColorConversionCodes.BGR2HSV);
                    bgr.Dispose();
                    break;
                case ColorSpaceType.LAB:
                    Cv2.CvtColor(bgr, colorSpaceMat, ColorConversionCodes.BGR2Lab);
                    bgr.Dispose();
                    break;
                default:
                    colorSpaceMat = bgr;
                    break;
            }
        }

        // Apply Gaussian blur
        Mat blurred = new Mat();
        Cv2.GaussianBlur(colorSpaceMat, blurred, new Size(BlurSizeX, BlurSizeY), BlurSigma);

        // Extract channel
        Mat blurredChannel = new Mat();
        if (blurred.Channels() == 3)
        {
            Cv2.ExtractChannel(blurred, blurredChannel, ChannelType);
        }
        else
        {
            blurredChannel = blurred.Clone();
        }

        // Find inner box
        var (innerB, innerG, innerR) = ColorUtils.HexToBgr(InnerBoxColorHex);
        var innerColor = new Scalar(innerB, innerG, innerR);
        RotatedRect innerBox = FindBox(blurredChannel, result, InnerThreshold, AreaMinSmallBox, innerColor);

        // Find outer box (with larger area min)
        var (outerB, outerG, outerR) = ColorUtils.HexToBgr(OuterBoxColorHex);
        var outerColor = new Scalar(outerB, outerG, outerR);
        RotatedRect outerBox = FindBox(blurredChannel, result, OuterThreshold, 40000, outerColor);

        double channel = GetChannelAvg(blurredChannel, outerBox);
        Cv2.PutText(result, $"Ch Avg: {channel:F2}", new Point(outerBox.Center.X, 30), HersheyFonts.HersheySimplex, 1, outerColor, 1);

        // Cleanup
        if (ColorSpace != ColorSpaceType.BGR || input.Channels() != 3)
        {
            colorSpaceMat.Dispose();
        }
        blurred.Dispose();
        blurredChannel.Dispose();

        return result;
    }

    private RotatedRect FindBox(Mat channelMat, Mat result, int threshValue, int areaMin, Scalar rectColor)
    {
        Mat? simpleThresh = null;
        Mat? kernel = null;
        Mat? morph = null;
        Mat? labels = null;
        Mat? stats = null;
        Mat? centroids = null;
        Mat? mask = null;

        try
        {
            // Apply threshold
            simpleThresh = new Mat();
            Cv2.Threshold(channelMat, simpleThresh, threshValue, 255, ThresholdTypes.Binary);

            // Morphological operation
            kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(MorphKernelSizeX, MorphKernelSizeY));
            morph = new Mat();
            Cv2.MorphologyEx(simpleThresh, morph, MorphTypes.Open, kernel);

            // Connected Components with Stats
            int areaMax = 150000;
            labels = new Mat();
            stats = new Mat();
            centroids = new Mat();
            int numLabels = Cv2.ConnectedComponentsWithStats(morph, labels, stats, centroids);

            // Find all valid labels
            mask = new Mat();
            RotatedRect? firstRect = null;
            
            for (int i = 1; i < numLabels; i++)
            {
                int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area > areaMin && area < areaMax)
                {
                    Cv2.Compare(labels, i, mask, CmpType.EQ);

                    try
                    {
                        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                        foreach (var contour in contours)
                        {
                            if (contour.Length > 0)
                            {
                                RotatedRect rect = Cv2.MinAreaRect(contour);
                                Point2f[] box = rect.Points();
                                Point[] intBox = Array.ConvertAll(box, p => new Point((int)p.X, (int)p.Y));

                                // Draw rotated rectangle
                                Cv2.DrawContours(result, new[] { intBox }, -1, rectColor, LineThickness);
                                Cv2.PutText(result, $"{rect.Size.Width}", new OpenCvSharp.Point(rect.Center.X, 75), HersheyFonts.HersheySimplex, 1, rectColor);

                                // Store first valid rectangle for return value
                                if (firstRect == null)
                                {
                                    firstRect = rect;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error finding box: {ex.Message}");
                    }
                }
            }

            return firstRect ?? new RotatedRect();
        }
        finally
        {
            // Cleanup all Mat objects
            simpleThresh?.Dispose();
            kernel?.Dispose();
            morph?.Dispose();
            labels?.Dispose();
            stats?.Dispose();
            centroids?.Dispose();
            mask?.Dispose();
        }
    }

    private double GetChannelAvg(Mat blurredChannel, RotatedRect outer)
    {
        if (outer.Size.Width == 0 || outer.Size.Height == 0) return 0;
        using var roi = new Mat(blurredChannel, outer.BoundingRect());

        Scalar mean = Cv2.Mean(roi);
        return mean.Val0;
    }
}

