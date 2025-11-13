using OpenCvSharp;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Helper class to store pattern detection results
/// </summary>
public class PatternDetectionResult
{
    /// <summary>
    /// Number of pattern instances detected
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Bounding boxes of detected patterns
    /// </summary>
    public List<Rect> BoundingBoxes { get; set; } = new List<Rect>();

    /// <summary>
    /// Confidence scores for each detection (0.0-1.0)
    /// </summary>
    public List<double> Confidences { get; set; } = new List<double>();

    /// <summary>
    /// Locations of detected patterns (top-left corners)
    /// </summary>
    public List<Point> Locations { get; set; } = new List<Point>();
}

