using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Line orientation filter options
/// </summary>
public enum LineOrientationFilter
{
    /// <summary>
    /// All lines (no filter)
    /// </summary>
    All,
    
    /// <summary>
    /// Horizontal lines only (within 5 degrees)
    /// </summary>
    Horizontal,
    
    /// <summary>
    /// Vertical lines only (within 5 degrees)
    /// </summary>
    Vertical
}

/// <summary>
/// Layer that uses Hough line detection (HoughLines and HoughLinesP) to detect lines and measure their total length
/// </summary>
public class HoughLineLayer : BaseLayer
{
    [Parameter(DisplayName = "Use Probabilistic Hough", Description = "Use HoughLinesP (probabilistic) instead of HoughLines (standard)", Order = 0)]
    public bool UseProbabilisticHough { get; set; } = true;

    [Parameter(DisplayName = "Line Orientation Filter", Description = "Filter lines by orientation", Order = 1)]
    public LineOrientationFilter OrientationFilter { get; set; } = LineOrientationFilter.All;

    [Parameter(DisplayName = "Threshold", Description = "Accumulator threshold (minimum votes for a line)", Order = 2)]
    [ParameterRange(1, 500, 1)]
    public int Threshold { get; set; } = 80;

    // HoughLinesP parameters
    [Parameter(DisplayName = "Min Line Length", Description = "Minimum line length in pixels (for probabilistic Hough)", Order = 3)]
    [ParameterRange(0, 1000, 1)]
    public int MinLineLength { get; set; } = 50;

    [Parameter(DisplayName = "Max Line Gap", Description = "Maximum gap between line segments to connect them (for probabilistic Hough)", Order = 4)]
    [ParameterRange(0, 100, 1)]
    public int MaxLineGap { get; set; } = 10;

    [Parameter(DisplayName = "Blur Size X", Description = "Gaussian blur kernel width (applied before edge detection)", Order = 5)]
    [ParameterRange(1, 100, 1)]
    public int BlurSizeX { get; set; } = 5;

    [Parameter(DisplayName = "Blur Size Y", Description = "Gaussian blur kernel height (applied before edge detection)", Order = 6)]
    [ParameterRange(1, 100, 1)]
    public int BlurSizeY { get; set; } = 5;

    [Parameter(DisplayName = "Blur Amount (Sigma)", Description = "Gaussian blur sigma value", Order = 7)]
    [ParameterRange(0.1, 50.0, 0.1)]
    public double BlurSigma { get; set; } = 1.0;

    [Parameter(DisplayName = "Canny Low Threshold", Description = "Lower threshold for Canny edge detection (used before Hough)", Order = 8)]
    [ParameterRange(0, 255, 1)]
    public int CannyLowThreshold { get; set; } = 50;

    [Parameter(DisplayName = "Canny High Threshold", Description = "Upper threshold for Canny edge detection (used before Hough)", Order = 9)]
    [ParameterRange(0, 255, 1)]
    public int CannyHighThreshold { get; set; } = 150;

    [Parameter(DisplayName = "Line Color", Description = "Color of the drawn lines in hex format", Order = 10)]
    [ParameterColor]
    public string LineColorHex { get; set; } = "#00FF00"; // Green

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of lines", Order = 11)]
    [ParameterRange(1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    // Constants for Hough transform
    private const double Rho = 1.0;
    private const double Theta = Math.PI / 180.0;
    private const double OrientationToleranceDegrees = 5.0;
    private const double OrientationToleranceRadians = OrientationToleranceDegrees * Math.PI / 180.0;

    public override string GetLayerType() => "HoughLine";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Prepare output for drawing
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result = PrepareDrawTarget(drawTarget);

        // Always convert to grayscale
        Mat gray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = input.Clone();
        }

        // Apply Gaussian blur
        Mat blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(BlurSizeX, BlurSizeY), BlurSigma);

        // Apply Canny edge detection (required for Hough line detection)
        Mat edges = new Mat();
        Cv2.Canny(blurred, edges, CannyLowThreshold, CannyHighThreshold);

        // Parse line color
        var (b, g, r) = ColorUtils.HexToBgr(LineColorHex);
        var lineColor = new Scalar(b, g, r);

        double totalLength = 0.0;
        int lineCount = 0;

        if (UseProbabilisticHough)
        {
            // Use HoughLinesP (probabilistic - returns line segments)
            LineSegmentPoint[] lines = Cv2.HoughLinesP(edges, Rho, Theta, Threshold, MinLineLength, MaxLineGap);

            foreach (var line in lines)
            {
                Point pt1 = new Point(line.P1.X, line.P1.Y);
                Point pt2 = new Point(line.P2.X, line.P2.Y);

                // Check orientation filter
                if (!MatchesOrientationFilter(pt1, pt2))
                    continue;

                // Calculate line length
                double length = Math.Sqrt(Math.Pow(pt2.X - pt1.X, 2) + Math.Pow(pt2.Y - pt1.Y, 2));

                // Draw line
                Cv2.Line(result, pt1, pt2, lineColor, LineThickness);

                totalLength += length;
                lineCount++;
            }
        }
        else
        {
            // Use HoughLines (standard - returns lines in polar coordinates)
            LineSegmentPolar[] lines = Cv2.HoughLines(edges, Rho, Theta, Threshold);

            int width = result.Width;
            int height = result.Height;

            foreach (var line in lines)
            {
                double rho = line.Rho;
                double theta = line.Theta;

                // Convert polar to Cartesian coordinates
                double cosTheta = Math.Cos(theta);
                double sinTheta = Math.Sin(theta);
                double x0 = rho * cosTheta;
                double y0 = rho * sinTheta;

                // Calculate line endpoints at image boundaries
                Point pt1, pt2;
                if (Math.Abs(sinTheta) < 0.001)
                {
                    // Horizontal line
                    pt1 = new Point(0, (int)y0);
                    pt2 = new Point(width, (int)y0);
                }
                else if (Math.Abs(cosTheta) < 0.001)
                {
                    // Vertical line
                    pt1 = new Point((int)x0, 0);
                    pt2 = new Point((int)x0, height);
                }
                else
                {
                    // Calculate intersections with image boundaries
                    double m = -cosTheta / sinTheta;
                    double lineB = rho / sinTheta;

                    // Intersection with left edge (x = 0)
                    int y1 = (int)lineB;
                    // Intersection with right edge (x = width)
                    int y2 = (int)(m * width + lineB);
                    // Intersection with top edge (y = 0)
                    int x1 = (int)(-lineB / m);
                    // Intersection with bottom edge (y = height)
                    int x2 = (int)((height - lineB) / m);

                    // Find valid intersections
                    var points = new List<Point>();
                    if (y1 >= 0 && y1 < height) points.Add(new Point(0, y1));
                    if (y2 >= 0 && y2 < height) points.Add(new Point(width, y2));
                    if (x1 >= 0 && x1 < width) points.Add(new Point(x1, 0));
                    if (x2 >= 0 && x2 < width) points.Add(new Point(x2, height));

                    if (points.Count >= 2)
                    {
                        pt1 = points[0];
                        pt2 = points[1];
                    }
                    else
                    {
                        // Fallback to center line
                        pt1 = new Point((int)x0, (int)y0);
                        pt2 = new Point((int)(x0 + width * cosTheta), (int)(y0 + height * sinTheta));
                    }
                }

                // Check orientation filter
                if (!MatchesOrientationFilter(pt1, pt2))
                    continue;

                // Calculate line length
                double length = Math.Sqrt(Math.Pow(pt2.X - pt1.X, 2) + Math.Pow(pt2.Y - pt1.Y, 2));

                // Draw line
                Cv2.Line(result, pt1, pt2, lineColor, LineThickness);

                totalLength += length;
                lineCount++;
            }
        }

        // Cleanup
        gray.Dispose();
        blurred.Dispose();
        edges.Dispose();

        return result;
    }

    private bool MatchesOrientationFilter(Point pt1, Point pt2)
    {
        if (OrientationFilter == LineOrientationFilter.All)
            return true;

        // Calculate angle of line
        double dx = pt2.X - pt1.X;
        double dy = pt2.Y - pt1.Y;
        double angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx));

        if (OrientationFilter == LineOrientationFilter.Horizontal)
        {
            // Horizontal: angle should be close to 0 (within tolerance)
            return angle <= OrientationToleranceRadians;
        }
        else if (OrientationFilter == LineOrientationFilter.Vertical)
        {
            // Vertical: angle should be close to π/2 (within tolerance)
            return Math.Abs(angle - Math.PI / 2.0) <= OrientationToleranceRadians;
        }

        return true;
    }
}

