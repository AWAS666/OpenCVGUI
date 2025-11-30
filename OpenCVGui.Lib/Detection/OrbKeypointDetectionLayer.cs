using OpenCvSharp;
using OpenCvSharp.Features2D;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that detects and displays ORB keypoints on an image
/// </summary>
public class OrbKeypointDetectionLayer : BaseLayer
{
    [Parameter(DisplayName = "Max Features", Description = "Maximum number of keypoints to detect", Order = 0)]
    [ParameterRange(100, 5000, 50)]
    public int MaxFeatures { get; set; } = 500;

    [Parameter(DisplayName = "Scale Factor", Description = "Pyramid decimation ratio (must be > 1.0, typically 1.2)", Order = 1)]
    [ParameterRange(1.1, 2.0, 0.1)]
    public double ScaleFactor { get; set; } = 1.2;

    [Parameter(DisplayName = "Edge Threshold", Description = "Size of the border where features are not detected (lower = more features near edges)", Order = 2)]
    [ParameterRange(1, 50, 1)]
    public int EdgeThreshold { get; set; } = 31;

    [Parameter(DisplayName = "Fast Threshold", Description = "FAST corner detection threshold (lower = more corners)", Order = 3)]
    [ParameterRange(1, 50, 1)]
    public int FastThreshold { get; set; } = 20;

    [Parameter(DisplayName = "Draw Keypoints", Description = "Draw detected keypoints on output", Order = 4)]
    public bool DrawKeypoints { get; set; } = true;

    [Parameter(DisplayName = "Keypoint Color", Description = "Color of drawn keypoints in hex format", Order = 5)]
    [ParameterColor]
    public string KeypointColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of keypoint circles and lines", Order = 6)]
    [ParameterRange(1, 5, 1)]
    public int LineThickness { get; set; } = 1;

    /// <summary>
    /// Number of keypoints detected in the last render
    /// </summary>
    public int KeypointCount { get; private set; }

    private ORB? _orbDetector;

    public override string GetLayerType() => "OrbKeypointDetection";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            KeypointCount = 0;
            return new Mat();
        }

        // Initialize ORB detector with custom parameters
        if (_orbDetector == null)
        {
            _orbDetector = ORB.Create(MaxFeatures);
            _orbDetector.ScaleFactor = ScaleFactor;
            _orbDetector.EdgeThreshold = EdgeThreshold;
            _orbDetector.FastThreshold = FastThreshold;
        }
        else
        {
            // Update detector if parameters changed
            if (_orbDetector.MaxFeatures != MaxFeatures ||
                Math.Abs(_orbDetector.ScaleFactor - ScaleFactor) > 0.01 ||
                _orbDetector.EdgeThreshold != EdgeThreshold ||
                _orbDetector.FastThreshold != FastThreshold)
            {
                _orbDetector.MaxFeatures = MaxFeatures;
                _orbDetector.ScaleFactor = ScaleFactor;
                _orbDetector.EdgeThreshold = EdgeThreshold;
                _orbDetector.FastThreshold = FastThreshold;
            }
        }

        // Convert to grayscale for ORB detection
        Mat gray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = input.Clone();
        }

        // Detect keypoints and descriptors
        KeyPoint[] keypoints;
        Mat descriptors = new Mat();
        _orbDetector.DetectAndCompute(gray, null, out keypoints, descriptors);
        gray.Dispose();
        descriptors.Dispose();

        KeypointCount = keypoints?.Length ?? 0;

        // Prepare output
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result = drawTarget != null && !drawTarget.Empty() ? drawTarget.Clone() : input.Clone();

        // Draw keypoints if enabled
        if (DrawKeypoints && keypoints != null && keypoints.Length > 0)
        {
            var (b, g, r) = ColorUtils.HexToBgr(KeypointColorHex);
            var keypointColor = new Scalar(b, g, r);

            // Draw keypoints (similar to cv2.drawKeypoints)
            foreach (var kp in keypoints)
            {
                Point center = new Point((int)kp.Pt.X, (int)kp.Pt.Y);
                int radius = (int)(kp.Size / 2.0);
                if (radius < 1) radius = 1;

                // Draw circle for keypoint
                Cv2.Circle(result, center, radius, keypointColor, LineThickness);

                // Draw orientation line if angle is valid
                if (kp.Angle >= 0)
                {
                    double angleRad = kp.Angle * Math.PI / 180.0;
                    Point endPoint = new Point(
                        (int)(center.X + radius * Math.Cos(angleRad)),
                        (int)(center.Y + radius * Math.Sin(angleRad))
                    );
                    Cv2.Line(result, center, endPoint, keypointColor, LineThickness);
                }
            }
        }

        return result;
    }
}
