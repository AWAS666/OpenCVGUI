using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Layer that calculates and displays image moments
/// </summary>
public class ImageMomentsLayer : BaseLayer
{
    [Parameter(DisplayName = "Binary Image", Description = "If true, treats input as binary image (finds moments of white pixels)", Order = 0)]
    public bool BinaryImage { get; set; } = true;

    [Parameter(DisplayName = "Draw Centroid", Description = "Draw the centroid (center of mass) on the image", Order = 1)]
    public bool DrawCentroid { get; set; } = true;

    [Parameter(DisplayName = "Centroid Color", Description = "Color of centroid marker in hex format", Order = 2)]
    [ParameterColor]
    public string CentroidColorHex { get; set; } = "#FF0000";

    [Parameter(DisplayName = "Marker Size", Description = "Size of centroid marker", Order = 3)]
    [ParameterRange(1, 20, 1)]
    public int MarkerSize { get; set; } = 5;

    /// <summary>
    /// Calculated centroid X coordinate
    /// </summary>
    public double CentroidX { get; private set; }

    /// <summary>
    /// Calculated centroid Y coordinate
    /// </summary>
    public double CentroidY { get; private set; }

    /// <summary>
    /// Calculated area (moment m00)
    /// </summary>
    public double Area { get; private set; }

    public override string GetLayerType() => "ImageMoments";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            CentroidX = 0;
            CentroidY = 0;
            Area = 0;
            return new Mat();
        }

        Mat processed = new Mat();

        if (BinaryImage)
        {
            // Convert to grayscale and threshold if needed
            if (input.Channels() == 3)
            {
                Mat gray = new Mat();
                Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(gray, processed, 127, 255, ThresholdTypes.Binary);
                gray.Dispose();
            }
            else
            {
                Cv2.Threshold(input, processed, 127, 255, ThresholdTypes.Binary);
            }
        }
        else
        {
            // Use input as-is
            if (input.Channels() == 3)
            {
                Cv2.CvtColor(input, processed, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                processed = input.Clone();
            }
        }

        // Calculate moments
        Moments moments = Cv2.Moments(processed);

        // Extract centroid and area
        Area = moments.M00;
        if (Area > 0)
        {
            CentroidX = moments.M10 / Area;
            CentroidY = moments.M01 / Area;
        }
        else
        {
            CentroidX = 0;
            CentroidY = 0;
        }

        // Prepare output
        Mat result;
        if (DrawCentroid && Area > 0)
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

            // Draw centroid
            var (b, g, r) = ColorUtils.HexToBgr(CentroidColorHex);
            var centroidColor = new Scalar(b, g, r);
            var centroid = new Point((int)CentroidX, (int)CentroidY);
            Cv2.Circle(result, centroid, MarkerSize, centroidColor, -1);
        }
        else
        {
            // Return processed result
            result = processed.Clone();
        }

        processed.Dispose();

        return result;
    }
}

