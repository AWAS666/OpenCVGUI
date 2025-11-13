using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that performs template matching to find and count occurrences of a pattern in an image
/// </summary>
public class TemplateMatchingLayer : BaseLayer
{
    [Parameter(DisplayName = "Template Image Path", Description = "Path to the template image file to search for", Order = 0)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    [ParameterImageSample]
    public string TemplateImagePath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Match Method", Description = "Template matching method to use", Order = 1)]
    public TemplateMatchModes MatchMethod { get; set; } = TemplateMatchModes.CCoeffNormed;

    [Parameter(DisplayName = "Match Threshold", Description = "Minimum match value to consider a match (0.0-1.0, higher = stricter)", Order = 2)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double MatchThreshold { get; set; } = 0.8;

    [Parameter(DisplayName = "Non-Maximum Suppression", Description = "Remove overlapping matches (keeps best match in overlapping regions)", Order = 3)]
    public bool UseNonMaxSuppression { get; set; } = true;

    [Parameter(DisplayName = "Overlap Threshold", Description = "Maximum overlap ratio for non-maximum suppression (0.0-1.0)", Order = 4)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double OverlapThreshold { get; set; } = 0.3;

    [Parameter(DisplayName = "Draw Matches", Description = "Draw bounding boxes around detected matches", Order = 5)]
    public bool DrawMatches { get; set; } = true;

    [Parameter(DisplayName = "Match Color", Description = "Color of match bounding boxes in hex format", Order = 6)]
    [ParameterColor]
    public string MatchColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of bounding box lines (-1 = filled)", Order = 7)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Number of matches found in the last render
    /// </summary>
    public int MatchCount { get; private set; }

    private Mat? _cachedTemplate;

    public override string GetLayerType() => "TemplateMatching";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            MatchCount = 0;
            return new Mat();
        }

        // Load template if path changed
        if (_cachedTemplate == null || !string.IsNullOrEmpty(TemplateImagePath))
        {
            LoadTemplate();
        }

        if (_cachedTemplate == null || _cachedTemplate.Empty())
        {
            MatchCount = 0;
            return input.Clone();
        }

        // Template must be smaller than source image
        if (_cachedTemplate.Width > input.Width || _cachedTemplate.Height > input.Height)
        {
            MatchCount = 0;
            return input.Clone();
        }

        // Perform template matching
        Mat resultMat = new Mat();
        Cv2.MatchTemplate(input, _cachedTemplate, resultMat, MatchMethod);

        // Find matches based on threshold
        List<Point> matchLocations = new List<Point>();
        List<double> matchValues = new List<double>();

        // Determine if higher or lower values indicate better matches
        bool higherIsBetter = MatchMethod == TemplateMatchModes.CCorr ||
                             MatchMethod == TemplateMatchModes.CCorrNormed ||
                             MatchMethod == TemplateMatchModes.CCoeff ||
                             MatchMethod == TemplateMatchModes.CCoeffNormed;

        // Find all matches above threshold
        for (int y = 0; y < resultMat.Height; y++)
        {
            for (int x = 0; x < resultMat.Width; x++)
            {
                double value = resultMat.Get<double>(y, x);
                bool isMatch = higherIsBetter ? value >= MatchThreshold : value <= MatchThreshold;

                if (isMatch)
                {
                    matchLocations.Add(new Point(x, y));
                    matchValues.Add(value);
                }
            }
        }

        // Apply non-maximum suppression if enabled
        if (UseNonMaxSuppression && matchLocations.Count > 0)
        {
            var filtered = ApplyNonMaxSuppression(matchLocations, matchValues, _cachedTemplate.Size(), OverlapThreshold, higherIsBetter);
            matchLocations = filtered.locations;
            matchValues = filtered.values;
        }

        MatchCount = matchLocations.Count;

        // Draw matches on output image
        Mat result = input.Clone();
        if (DrawMatches)
        {
            var (b, g, r) = ColorUtils.HexToBgr(MatchColorHex);
            var matchColor = new Scalar(b, g, r);
            foreach (var location in matchLocations)
            {
                var rect = new Rect(location, _cachedTemplate.Size());
                Cv2.Rectangle(result, rect, matchColor, LineThickness);
            }
        }

        resultMat.Dispose();

        return result;
    }

    private void LoadTemplate()
    {
        if (_cachedTemplate != null)
        {
            _cachedTemplate.Dispose();
            _cachedTemplate = null;
        }

        if (string.IsNullOrEmpty(TemplateImagePath) || !File.Exists(TemplateImagePath))
        {
            return;
        }

        _cachedTemplate = Cv2.ImRead(TemplateImagePath, ImreadModes.Color);
        if (_cachedTemplate != null && _cachedTemplate.Empty())
        {
            _cachedTemplate.Dispose();
            _cachedTemplate = null;
        }
    }

    private (List<Point> locations, List<double> values) ApplyNonMaxSuppression(
        List<Point> locations, List<double> values, Size templateSize, double overlapThreshold, bool higherIsBetter)
    {
        if (locations.Count == 0)
            return (new List<Point>(), new List<double>());

        // Create list of matches with their bounding boxes
        var matches = new List<(Point location, double value, Rect rect)>();
        for (int i = 0; i < locations.Count; i++)
        {
            matches.Add((locations[i], values[i], new Rect(locations[i], templateSize)));
        }

        // Sort by match quality (best first)
        matches.Sort((a, b) => higherIsBetter ? b.value.CompareTo(a.value) : a.value.CompareTo(b.value));

        // Apply non-maximum suppression
        List<Point> filteredLocations = new List<Point>();
        List<double> filteredValues = new List<double>();

        while (matches.Count > 0)
        {
            // Take the best match
            var best = matches[0];
            filteredLocations.Add(best.location);
            filteredValues.Add(best.value);

            // Remove overlapping matches
            matches.RemoveAt(0);
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                double overlap = CalculateOverlap(best.rect, matches[i].rect);
                if (overlap > overlapThreshold)
                {
                    matches.RemoveAt(i);
                }
            }
        }

        return (filteredLocations, filteredValues);
    }

    private double CalculateOverlap(Rect rect1, Rect rect2)
    {
        var intersection = rect1 & rect2;
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0.0;

        double intersectionArea = intersection.Width * intersection.Height;
        double unionArea = rect1.Width * rect1.Height + rect2.Width * rect2.Height - intersectionArea;

        return intersectionArea / unionArea;
    }

    public void DisposeTemplate()
    {
        _cachedTemplate?.Dispose();
        _cachedTemplate = null;
    }
}

