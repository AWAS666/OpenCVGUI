using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that computes and draws the convex hull of contours
/// </summary>
public class ConvexHullLayer : BaseLayer
{
    [Parameter(DisplayName = "Min Area", Description = "Minimum contour area to process (smaller contours are ignored)", Order = 0)]
    [ParameterRange(0, 100000, 1)]
    public double MinArea { get; set; } = 100.0;

    [Parameter(DisplayName = "Max Area", Description = "Maximum contour area to process (0 = no limit)", Order = 1)]
    [ParameterRange(0, 1000000, 1)]
    public double MaxArea { get; set; } = 0.0;

    [Parameter(DisplayName = "Draw on Original", Description = "If true, draws convex hull on original image; if false, creates blank image", Order = 2)]
    public bool DrawOnOriginal { get; set; } = true;

    [Parameter(DisplayName = "Draw Contours", Description = "Draw original contours in addition to convex hull", Order = 3)]
    public bool DrawContours { get; set; } = false;

    [Parameter(DisplayName = "Hull Color", Description = "Color of convex hull in hex format", Order = 4)]
    [ParameterColor]
    public string HullColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Contour Color", Description = "Color of original contours in hex format", Order = 5)]
    [ParameterColor]
    public string ContourColorHex { get; set; } = "#FF0000";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of lines (-1 = filled)", Order = 6)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    [Parameter(DisplayName = "Clockwise", Description = "If true, output convex hull points in clockwise order", Order = 7)]
    public bool Clockwise { get; set; } = false;

    /// <summary>
    /// Number of convex hulls computed in the last render
    /// </summary>
    public int HullCount { get; private set; }

    public override string GetLayerType() => "ConvexHull";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            HullCount = 0;
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

        // Apply threshold to get binary image
        Mat binary = new Mat();
        Cv2.Threshold(gray, binary, 127, 255, ThresholdTypes.Binary);

        // Find contours
        Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // Prepare output
        Mat result;
        if (DrawOnOriginal)
        {
            result = input.Clone();
        }
        else
        {
            result = new Mat(input.Size(), input.Type(), Scalar.All(0));
        }

        // Parse colors
        var (hullB, hullG, hullR) = ColorUtils.HexToBgr(HullColorHex);
        var hullColor = new Scalar(hullB, hullG, hullR);

        var (contourB, contourG, contourR) = ColorUtils.HexToBgr(ContourColorHex);
        var contourColor = new Scalar(contourB, contourG, contourR);

        int hullCount = 0;

        // Compute and draw convex hull for each contour
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area >= MinArea && (MaxArea <= 0 || area <= MaxArea))
            {
                // Draw original contour if requested
                if (DrawContours)
                {
                    Cv2.DrawContours(result, new[] { contour }, -1, contourColor, LineThickness);
                }

                // Compute convex hull
                Point[] hull = Cv2.ConvexHull(contour, Clockwise);

                // Draw convex hull
                Cv2.DrawContours(result, new[] { hull }, -1, hullColor, LineThickness);

                hullCount++;
            }
        }

        HullCount = hullCount;

        // Cleanup
        gray.Dispose();
        binary.Dispose();

        return result;
    }
}

