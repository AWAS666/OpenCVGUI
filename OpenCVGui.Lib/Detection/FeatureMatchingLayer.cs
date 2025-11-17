using OpenCvSharp;
using OpenCvSharp.Features2D;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

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

    [Parameter(DisplayName = "Scale Factor", Description = "Pyramid decimation ratio (must be > 1.0, typically 1.2)", Order = 2)]
    [ParameterRange(1.1, 2.0, 0.1)]
    public double ScaleFactor { get; set; } = 1.2;

    [Parameter(DisplayName = "Edge Threshold", Description = "Size of the border where features are not detected (lower = more features near edges)", Order = 3)]
    [ParameterRange(1, 50, 1)]
    public int EdgeThreshold { get; set; } = 31;

    [Parameter(DisplayName = "Fast Threshold", Description = "FAST corner detection threshold (lower = more corners)", Order = 4)]
    [ParameterRange(1, 50, 1)]
    public int FastThreshold { get; set; } = 20;

    [Parameter(DisplayName = "Enable Preprocessing", Description = "Apply CLAHE contrast enhancement to help detect features in low-contrast images", Order = 5)]
    public bool EnablePreprocessing { get; set; } = false;

    [Parameter(DisplayName = "Match Ratio", Description = "Lowe's ratio test threshold for filtering matches (0.0-1.0, lower = stricter)", Order = 6)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double MatchRatio { get; set; } = 0.75;

    [Parameter(DisplayName = "Min Matches", Description = "Minimum number of good matches required to consider a pattern instance", Order = 7)]
    [ParameterRange(4, 100, 1)]
    public int MinMatches { get; set; } = 10;

    [Parameter(DisplayName = "RANSAC Threshold", Description = "Maximum distance for RANSAC inlier classification (pixels)", Order = 8)]
    [ParameterRange(1.0, 50.0, 1.0)]
    public double RansacThreshold { get; set; } = 5.0;

    [Parameter(DisplayName = "Draw Matches", Description = "Draw bounding boxes around detected pattern instances", Order = 9)]
    public bool DrawMatches { get; set; } = true;

    [Parameter(DisplayName = "Match Color", Description = "Color of match bounding boxes in hex format", Order = 10)]
    [ParameterColor]
    public string MatchColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of bounding box lines (-1 = filled)", Order = 11)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Number of pattern instances found in the last render
    /// </summary>
    public int MatchCount { get; private set; }

    /// <summary>
    /// Number of features detected in the template image
    /// </summary>
    public int TemplateFeatureCount { get; private set; }

    private Mat? _cachedTemplate;
    private KeyPoint[]? _templateKeypoints;
    private Mat? _templateDescriptors;
    private ORB? _orbDetector;
    private string _lastLoadedTemplatePath = string.Empty;

    public override string GetLayerType() => "FeatureMatching";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            MatchCount = 0;
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

        // Check if template path is provided
        bool hasTemplate = !string.IsNullOrEmpty(TemplateImagePath) && File.Exists(TemplateImagePath);
        
        if (hasTemplate)
        {
            // Load and process template if needed or if path changed
            if (_cachedTemplate == null || _templateKeypoints == null || _templateDescriptors == null ||
                _lastLoadedTemplatePath != TemplateImagePath)
            {
                LoadAndProcessTemplate();
            }

            // Throw error if no features detected in template
            if (_cachedTemplate == null || _cachedTemplate.Empty() || 
                _templateKeypoints == null || _templateDescriptors == null || _templateDescriptors.Empty())
            {
                throw new InvalidOperationException(
                    $"No features detected in template image '{TemplateImagePath}'. " +
                    "Try: enabling preprocessing, lowering Fast Threshold, lowering Edge Threshold, or using a template with more texture.");
            }
        }
        else
        {
            // No template - just detect and display keypoints on input image
            return DetectAndDrawKeypoints(input, renderMode, chainInput);
        }

        // Convert source to grayscale for feature detection (consistent with template)
        Mat sourceGray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, sourceGray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            sourceGray = input.Clone();
        }

        // Apply preprocessing if enabled (consistent with template)
        Mat processedSource = sourceGray;
        if (EnablePreprocessing)
        {
            using (var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, new Size(8, 8)))
            {
                processedSource = new Mat();
                clahe.Apply(sourceGray, processedSource);
            }
            sourceGray.Dispose();
        }

        // Detect features in source image
        KeyPoint[] sourceKeypoints;
        Mat sourceDescriptors = new Mat();
        _orbDetector.DetectAndCompute(processedSource, null, out sourceKeypoints, sourceDescriptors);
        processedSource.Dispose();

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
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result = drawTarget != null && !drawTarget.Empty() ? drawTarget.Clone() : input.Clone();
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
            var (b, g, r) = ColorUtils.HexToBgr(MatchColorHex);
            var matchColor = new Scalar(b, g, r);
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

        Mat loadedTemplate = Cv2.ImRead(TemplateImagePath, ImreadModes.Color);
        if (loadedTemplate == null || loadedTemplate.Empty())
        {
            loadedTemplate?.Dispose();
            return;
        }

        // Convert to grayscale for better feature detection (ORB works best on grayscale)
        Mat templateGray = new Mat();
        if (loadedTemplate.Channels() == 3)
        {
            Cv2.CvtColor(loadedTemplate, templateGray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            templateGray = loadedTemplate.Clone();
        }
        loadedTemplate.Dispose();

        // Check minimum size - ORB needs at least a few pixels to detect features
        if (templateGray.Width < 8 || templateGray.Height < 8)
        {
            templateGray.Dispose();
            return;
        }

        // Apply preprocessing if enabled (CLAHE for contrast enhancement)
        Mat processedTemplate = templateGray;
        if (EnablePreprocessing)
        {
            using (var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, new Size(8, 8)))
            {
                processedTemplate = new Mat();
                clahe.Apply(templateGray, processedTemplate);
            }
            templateGray.Dispose();
        }

        _cachedTemplate = processedTemplate;

        // Ensure detector is initialized (should already be done in RenderInternal, but safety check)
        if (_orbDetector == null)
        {
            _orbDetector = ORB.Create(MaxFeatures);
            _orbDetector.ScaleFactor = ScaleFactor;
            _orbDetector.EdgeThreshold = EdgeThreshold;
            _orbDetector.FastThreshold = FastThreshold;
        }

        _templateDescriptors = new Mat();
        _orbDetector.DetectAndCompute(_cachedTemplate, null, out _templateKeypoints, _templateDescriptors);
        
        // Store feature count for debugging
        TemplateFeatureCount = _templateKeypoints?.Length ?? 0;
        
        // Validate that we actually got features - throw error if none found
        if (_templateKeypoints == null || _templateKeypoints.Length == 0 || 
            _templateDescriptors == null || _templateDescriptors.Empty())
        {
            // Clean up if no features found
            _templateKeypoints = null;
            _templateDescriptors?.Dispose();
            _templateDescriptors = null;
            TemplateFeatureCount = 0;
            
            throw new InvalidOperationException(
                $"No features detected in template image '{TemplateImagePath}'. " +
                "Try: enabling preprocessing, lowering Fast Threshold, lowering Edge Threshold, or using a template with more texture.");
        }
        
        _lastLoadedTemplatePath = TemplateImagePath;
    }

    private Mat DetectAndDrawKeypoints(Mat input, RenderMode renderMode, Mat? chainInput)
    {
        // Convert to grayscale
        Mat sourceGray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, sourceGray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            sourceGray = input.Clone();
        }

        // Apply preprocessing if enabled
        Mat processedSource = sourceGray;
        if (EnablePreprocessing)
        {
            using (var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, new Size(8, 8)))
            {
                processedSource = new Mat();
                clahe.Apply(sourceGray, processedSource);
            }
            sourceGray.Dispose();
        }

        // Detect features
        KeyPoint[] keypoints;
        Mat descriptors = new Mat();
        if (_orbDetector == null)
        {
            _orbDetector = ORB.Create(MaxFeatures);
            _orbDetector.ScaleFactor = ScaleFactor;
            _orbDetector.EdgeThreshold = EdgeThreshold;
            _orbDetector.FastThreshold = FastThreshold;
        }
        _orbDetector.DetectAndCompute(processedSource, null, out keypoints, descriptors);
        processedSource.Dispose();
        descriptors.Dispose();

        // Draw keypoints on result
        Mat? drawTarget = GetDrawTarget(input, renderMode, chainInput);
        Mat result = drawTarget != null && !drawTarget.Empty() ? drawTarget.Clone() : input.Clone();
        if (keypoints != null && keypoints.Length > 0)
        {
            var (b, g, r) = ColorUtils.HexToBgr(MatchColorHex);
            var keypointColor = new Scalar(b, g, r);
            
            // Draw keypoints
            foreach (var kp in keypoints)
            {
                Point center = new Point((int)kp.Pt.X, (int)kp.Pt.Y);
                int radius = (int)(kp.Size / 2.0);
                if (radius < 1) radius = 1;
                
                // Draw circle
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

        MatchCount = 0;
        TemplateFeatureCount = keypoints?.Length ?? 0;
        
        return result;
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
        _lastLoadedTemplatePath = string.Empty;
        TemplateFeatureCount = 0;
    }
}

