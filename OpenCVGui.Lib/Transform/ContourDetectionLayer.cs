using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

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

    [Parameter(DisplayName = "Contour Color (B)", Description = "Blue component of contour color (0-255)", Order = 3)]
    [ParameterRange(0, 255, 1)]
    public int ContourColorB { get; set; } = 0;

    [Parameter(DisplayName = "Contour Color (G)", Description = "Green component of contour color (0-255)", Order = 4)]
    [ParameterRange(0, 255, 1)]
    public int ContourColorG { get; set; } = 255;

    [Parameter(DisplayName = "Contour Color (R)", Description = "Red component of contour color (0-255)", Order = 5)]
    [ParameterRange(0, 255, 1)]
    public int ContourColorR { get; set; } = 0;

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of contour lines (-1 = filled)", Order = 6)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    [Parameter(DisplayName = "Retrieval Mode", Description = "Contour retrieval mode", Order = 7)]
    public RetrievalModes RetrievalMode { get; set; } = RetrievalModes.External;

    [Parameter(DisplayName = "Approximation Mode", Description = "Contour approximation mode", Order = 8)]
    public ContourApproximationModes ApproximationMode { get; set; } = ContourApproximationModes.ApproxSimple;

    public override string GetLayerType() => "ContourDetection";

    protected override Mat RenderInternal(Mat? input)
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

        // Apply threshold to get binary image
        Mat binary = new Mat();
        Cv2.Threshold(gray, binary, 127, 255, ThresholdTypes.Binary);

        // Find contours
        Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalMode, ApproximationMode);

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

        // Draw contours
        var contourColor = new Scalar(ContourColorB, ContourColorG, ContourColorR);
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area >= MinArea && (MaxArea <= 0 || area <= MaxArea))
            {
                Cv2.DrawContours(result, new[] { contour }, -1, contourColor, LineThickness);
            }
        }

        gray.Dispose();
        binary.Dispose();

        return result;
    }
}

