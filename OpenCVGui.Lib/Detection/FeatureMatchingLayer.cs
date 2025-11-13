using OpenCvSharp;
using OpenCvSharp.Features2D;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that performs feature-based pattern matching using ORB detector for robust pattern detection
/// </summary>
public class FeatureMatchingLayer : BaseLayer
{
    [Parameter(DisplayName = "Template Image Path", Description = "Path to the template image file to search for", Order = 0)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    [ParameterImageSample]
    public string TemplateImagePath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Max Features", Description = "Maximum number of features to detect in each image", Order = 1)]
    [ParameterRange(100, 5000, 50)]
    public int MaxFeatures { get; set; } = 500;

    [Parameter(DisplayName = "Match Ratio", Description = "Lowe's ratio test threshold for filtering matches (0.0-1.0, lower = stricter)", Order = 2)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double MatchRatio { get; set; } = 0.75;

    [Parameter(DisplayName = "Min Matches", Description = "Minimum number of good matches required to consider a pattern instance", Order = 3)]
    [ParameterRange(4, 100, 1)]
    public int MinMatches { get; set; } = 10;

    [Parameter(DisplayName = "RANSAC Threshold", Description = "Maximum distance for RANSAC inlier classification (pixels)", Order = 4)]
    [ParameterRange(1.0, 50.0, 1.0)]
    public double RansacThreshold { get; set; } = 5.0;

    [Parameter(DisplayName = "Draw Matches", Description = "Draw bounding boxes around detected pattern instances", Order = 5)]
    public bool DrawMatches { get; set; } = true;

    [Parameter(DisplayName = "Match Color (B)", Description = "Blue component of match bounding box color (0-255)", Order = 6)]
    [ParameterRange(0, 255, 1)]
    public int MatchColorB { get; set; } = 0;

    [Parameter(DisplayName = "Match Color (G)", Description = "Green component of match bounding box color (0-255)", Order = 7)]
    [ParameterRange(0, 255, 1)]
    public int MatchColorG { get; set; } = 255;

    [Parameter(DisplayName = "Match Color (R)", Description = "Red component of match bounding box color (0-255)", Order = 8)]
    [ParameterRange(0, 255, 1)]
    public int MatchColorR { get; set; } = 0;

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of bounding box lines (-1 = filled)", Order = 9)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Number of pattern instances found in the last render
    /// </summary>
    public int MatchCount { get; private set; }

    private Mat? _cachedTemplate;
    private KeyPoint[]? _templateKeypoints;
    private Mat? _templateDescriptors;
    private ORB? _orbDetector;

    public override string GetLayerType() => "FeatureMatching";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            MatchCount = 0;
            return new Mat();
        }

        // Initialize ORB detector
        if (_orbDetector == null)
        {
            _orbDetector = ORB.Create(MaxFeatures);
        }
        else
        {
            _orbDetector.MaxFeatures = MaxFeatures;
        }

        // Load and process template if needed
        if (_cachedTemplate == null || _templateKeypoints == null || _templateDescriptors == null)
        {
            LoadAndProcessTemplate();
        }

        if (_cachedTemplate == null || _cachedTemplate.Empty() || 
            _templateKeypoints == null || _templateDescriptors == null || _templateDescriptors.Empty())
        {
            MatchCount = 0;
            return input.Clone();
        }

        // Detect features in source image
        KeyPoint[] sourceKeypoints;
        Mat sourceDescriptors = new Mat();
        _orbDetector.DetectAndCompute(input, null, out sourceKeypoints, sourceDescriptors);

        if (sourceKeypoints.Length < MinMatches || sourceDescriptors.Empty())
        {
            sourceDescriptors.Dispose();
            MatchCount = 0;
            return input.Clone();
        }

        // Match features
        var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
        DMatch[][] knnMatches = matcher.KnnMatch(_templateDescriptors, sourceDescriptors, k: 2);

        // Apply Lowe's ratio test
        List<DMatch> goodMatches = new List<DMatch>();
        foreach (var matchPair in knnMatches)
        {
            if (matchPair.Length == 2)
            {
                if (matchPair[0].Distance < MatchRatio * matchPair[1].Distance)
                {
                    goodMatches.Add(matchPair[0]);
                }
            }
            else if (matchPair.Length == 1)
            {
                goodMatches.Add(matchPair[0]);
            }
        }

        if (goodMatches.Count < MinMatches)
        {
            sourceDescriptors.Dispose();
            MatchCount = 0;
            return input.Clone();
        }

        // Extract matched points
        Point2f[] templatePoints = new Point2f[goodMatches.Count];
        Point2f[] sourcePoints = new Point2f[goodMatches.Count];

        for (int i = 0; i < goodMatches.Count; i++)
        {
            templatePoints[i] = _templateKeypoints[goodMatches[i].QueryIdx].Pt;
            sourcePoints[i] = sourceKeypoints[goodMatches[i].TrainIdx].Pt;
        }

        // Find homography using RANSAC
        Mat homography = new Mat();
        Mat mask = new Mat();
        InputArray templatePointsInput = InputArray.Create(templatePoints);
        InputArray sourcePointsInput = InputArray.Create(sourcePoints);
        Cv2.FindHomography(templatePointsInput, sourcePointsInput, HomographyMethods.Ransac, RansacThreshold, mask);
        templatePointsInput.Dispose();
        sourcePointsInput.Dispose();

        // Count inliers (valid matches according to RANSAC)
        int inlierCount = 0;
        if (!mask.Empty())
        {
            for (int i = 0; i < mask.Rows; i++)
            {
                if (mask.Get<byte>(i, 0) != 0)
                {
                    inlierCount++;
                }
            }
        }

        // If we have enough inliers, we found a pattern instance
        MatchCount = inlierCount >= MinMatches ? 1 : 0;

        // Draw bounding box if match found
        Mat result = input.Clone();
        if (DrawMatches && MatchCount > 0 && !homography.Empty())
        {
            // Get template corners
            Point2f[] templateCorners = new Point2f[]
            {
                new Point2f(0, 0),
                new Point2f(_cachedTemplate.Width, 0),
                new Point2f(_cachedTemplate.Width, _cachedTemplate.Height),
                new Point2f(0, _cachedTemplate.Height)
            };

            // Transform corners to source image
            Point2f[] sourceCorners = Cv2.PerspectiveTransform(templateCorners, homography);

            // Draw bounding box
            var matchColor = new Scalar(MatchColorB, MatchColorG, MatchColorR);
            Point[] corners = Array.ConvertAll(sourceCorners, p => new Point((int)p.X, (int)p.Y));
            Cv2.Polylines(result, new[] { corners }, true, matchColor, LineThickness);
        }

        sourceDescriptors.Dispose();
        homography.Dispose();
        mask.Dispose();

        return result;
    }

    private void LoadAndProcessTemplate()
    {
        if (_cachedTemplate != null)
        {
            _cachedTemplate.Dispose();
            _cachedTemplate = null;
        }

        _templateKeypoints = null;
        if (_templateDescriptors != null)
        {
            _templateDescriptors.Dispose();
            _templateDescriptors = null;
        }

        if (string.IsNullOrEmpty(TemplateImagePath) || !File.Exists(TemplateImagePath))
        {
            return;
        }

        _cachedTemplate = Cv2.ImRead(TemplateImagePath, ImreadModes.Color);
        if (_cachedTemplate == null || _cachedTemplate.Empty())
        {
            if (_cachedTemplate != null)
            {
                _cachedTemplate.Dispose();
                _cachedTemplate = null;
            }
            return;
        }

        // Detect features in template
        if (_orbDetector == null)
        {
            _orbDetector = ORB.Create(MaxFeatures);
        }

        _templateDescriptors = new Mat();
        _orbDetector.DetectAndCompute(_cachedTemplate, null, out _templateKeypoints, _templateDescriptors);
    }

    public void DisposeTemplate()
    {
        _cachedTemplate?.Dispose();
        _cachedTemplate = null;
        _templateKeypoints = null;
        _templateDescriptors?.Dispose();
        _templateDescriptors = null;
        _orbDetector?.Dispose();
        _orbDetector = null;
    }
}

