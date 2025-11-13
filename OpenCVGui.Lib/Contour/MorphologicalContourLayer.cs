using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that performs morphological operations on a mask and draws bounding rectangles for detected contours
/// </summary>
public class MorphologicalContourLayer : BaseLayer
{
    [Parameter(DisplayName = "Kernel Size", Description = "Size of the morphological kernel (width, height)", Order = 0)]
    [ParameterRange(1, 50, 1)]
    public int KernelSize { get; set; } = 1;

    [Parameter(DisplayName = "Erode Iterations", Description = "Number of erosion iterations", Order = 1)]
    [ParameterRange(0, 20, 1)]
    public int ErodeIterations { get; set; } = 1;

    [Parameter(DisplayName = "Min Area", Description = "Minimum contour area to detect (smaller contours are ignored)", Order = 2)]
    [ParameterRange(0, 100000, 1)]
    public double MinArea { get; set; } = 100.0;

    [Parameter(DisplayName = "Max Area", Description = "Maximum contour area to detect (0 = no limit)", Order = 3)]
    [ParameterRange(0, 1000000, 1)]
    public double MaxArea { get; set; } = 0.0;

    [Parameter(DisplayName = "Rectangle Color", Description = "Color of the drawn rectangles in hex format (e.g., #00FF00 for green)", Order = 4)]
    [ParameterColor]
    public string RectangleColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of rectangle lines (-1 = filled)", Order = 5)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 1;

    public override string GetLayerType() => "MorphologicalContour";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // In UI mode, use chainInput for processing (mask) if available,
        // otherwise fall back to converting input to grayscale
        Mat mask = new Mat();
        if (renderMode == RenderMode.UI && chainInput != null && !chainInput.Empty())
        {
            // Use chain output (mask) for processing
            if (chainInput.Channels() == 1)
            {
                mask = chainInput.Clone();
            }
            else
            {
                Cv2.CvtColor(chainInput, mask, ColorConversionCodes.BGR2GRAY);
            }
        }
        else
        {
            // Convert input to grayscale for mask operations
            if (input.Channels() == 3)
            {
                Cv2.CvtColor(input, mask, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                mask = input.Clone();
            }
        }

        // Create kernel for morphological operations
        Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(KernelSize, KernelSize));

        // Morphological opening
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);

        // Erosion
        if (ErodeIterations > 0)
        {
            Cv2.Erode(mask, mask, kernel, iterations: ErodeIterations);
        }

        // Find contours
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // Prepare output - convert to BGR if needed for colored rectangles
        Mat result;
        if (input.Channels() == 3)
        {
            result = input.Clone();
        }
        else
        {
            // Convert grayscale to BGR for colored rectangles
            result = new Mat();
            Cv2.CvtColor(input, result, ColorConversionCodes.GRAY2BGR);
        }

        // Draw rectangles for contours that meet area criteria
        var (b, g, r) = ColorUtils.HexToBgr(RectangleColorHex);
        var rectangleColor = new Scalar(b, g, r);
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area >= MinArea && (MaxArea <= 0 || area <= MaxArea))
            {
                var rect = Cv2.BoundingRect(contour);
                Cv2.Rectangle(result, rect, rectangleColor, LineThickness);
            }
        }

        // Cleanup
        mask.Dispose();
        kernel.Dispose();

        return result;
    }
}

