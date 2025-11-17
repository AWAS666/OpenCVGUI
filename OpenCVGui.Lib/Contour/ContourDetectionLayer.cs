using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that detects contours in an image and draws them on the output
/// </summary>
public class ContourDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Min Area", Description = "Minimum contour area to detect (smaller contours are ignored)", Order = 0)]
    [ParameterRange(0, 100000, 1)]
    public double MinArea { get; set; } = 100.0;

    [Parameter(DisplayName = "Max Area", Description = "Maximum contour area to detect (0 = no limit)", Order = 1)]
    [ParameterRange(0, 1000000, 1)]
    public double MaxArea { get; set; } = 0.0;

    [Parameter(DisplayName = "Draw on Original", Description = "If true, draws contours on the original image; if false, creates a blank image with contours", Order = 2)]
    public bool DrawOnOriginal { get; set; } = true;

    [Parameter(DisplayName = "Contour Color", Description = "Color of contours in hex format", Order = 3)]
    [ParameterColor]
    public string ContourColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of contour lines (-1 = filled)", Order = 4)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    [Parameter(DisplayName = "Retrieval Mode", Description = "Contour retrieval mode", Order = 5)]
    public RetrievalModes RetrievalMode { get; set; } = RetrievalModes.External;

    [Parameter(DisplayName = "Approximation Mode", Description = "Contour approximation mode", Order = 6)]
    public ContourApproximationModes ApproximationMode { get; set; } = ContourApproximationModes.ApproxSimple;

    [Parameter(DisplayName = "Draw Rotated Rectangle", Description = "If true, draws minimum area rotated rectangle instead of full contour", Order = 7)]
    public bool DrawRotatedRectangle { get; set; } = false;

    public override string GetLayerType() => "ContourDetection";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Processing: always use input (chain output from previous layers)
        // This is what most layers work on
        Mat processingMat = new Mat();
        if (input.Channels() == 1)
        {
            processingMat = input.Clone();
        }
        else
        {
            Cv2.CvtColor(input, processingMat, ColorConversionCodes.BGR2GRAY);
        }

        // Apply threshold to get binary image
        Mat binary = new Mat();
        Cv2.Threshold(processingMat, binary, 127, 255, ThresholdTypes.Binary);

        // Find contours
        Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalMode, ApproximationMode);

        // Prepare output for drawing
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result;
        if (DrawOnOriginal)
        {
            result = PrepareDrawTarget(drawTarget);
        }
        else
        {
            if (drawTarget != null && !drawTarget.Empty())
            {
                if (drawTarget.Channels() == 3)
                {
                    result = new Mat(drawTarget.Size(), drawTarget.Type(), Scalar.All(0));
                }
                else
                {
                    result = new Mat(drawTarget.Size(), MatType.CV_8UC3, Scalar.All(0));
                }
            }
            else
            {
                result = new Mat();
            }
        }

        // Draw contours
        var (b, g, r) = ColorUtils.HexToBgr(ContourColorHex);
        var contourColor = new Scalar(b, g, r);
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area >= MinArea && (MaxArea <= 0 || area <= MaxArea))
            {
                if (DrawRotatedRectangle)
                {
                    RotatedRect rect = Cv2.MinAreaRect(contour);
                    Point2f[] box = rect.Points();
                    Point[] intBox = Array.ConvertAll(box, p => new Point((int)p.X, (int)p.Y));
                    Cv2.DrawContours(result, new[] { intBox }, -1, contourColor, LineThickness);
                    Cv2.PutText(result, $"{rect.Size.Width}", new Point(rect.Center.X, rect.Center.Y-rect.Size.Height), HersheyFonts.HersheySimplex, 1, contourColor);
                }
                else
                {
                    Cv2.DrawContours(result, new[] { contour }, -1, contourColor, LineThickness);
                }
            }
        }

        processingMat.Dispose();
        binary.Dispose();

        return result;
    }
}

