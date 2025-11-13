using OpenCvSharp;
using OpenCvSharp.ML;
using OpenCVGui.Lib.Attributes;
using OpenCVGui.Lib.Transform;

namespace OpenCVGui.Lib.Detection;

/// <summary>
/// Layer that learns patterns from examples using machine learning (SVM or KNN) and detects them in images
/// </summary>
public class PatternLearningLayer : BaseLayer
{
    [Parameter(DisplayName = "Learning Mode", Description = "Current mode: Training or Detection", Order = 0)]
    public PatternLearningMode Mode { get; set; } = PatternLearningMode.Detection;

    [Parameter(DisplayName = "Algorithm", Description = "Machine learning algorithm to use", Order = 1)]
    public PatternLearningAlgorithm Algorithm { get; set; } = PatternLearningAlgorithm.SVM;

    [Parameter(DisplayName = "Model Path", Description = "Path to save/load trained model", Order = 2)]
    [ParameterFile(Filter = "Model files|*.xml;*.yaml")]
    public string ModelPath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Positive Samples Path", Description = "Path to directory containing positive training samples (pattern images)", Order = 3)]
    public string PositiveSamplesPath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Negative Samples Path", Description = "Path to directory containing negative training samples (non-pattern images)", Order = 4)]
    public string NegativeSamplesPath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Window Width", Description = "Width of sliding window for detection (pixels)", Order = 5)]
    [ParameterRange(16, 512, 8)]
    public int WindowWidth { get; set; } = 64;

    [Parameter(DisplayName = "Window Height", Description = "Height of sliding window for detection (pixels)", Order = 6)]
    [ParameterRange(16, 512, 8)]
    public int WindowHeight { get; set; } = 64;

    [Parameter(DisplayName = "Step Size", Description = "Step size for sliding window (pixels)", Order = 7)]
    [ParameterRange(1, 64, 1)]
    public int StepSize { get; set; } = 8;

    [Parameter(DisplayName = "Detection Threshold", Description = "Minimum confidence score for detection (0.0-1.0)", Order = 8)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double DetectionThreshold { get; set; } = 0.5;

    [Parameter(DisplayName = "Non-Maximum Suppression", Description = "Remove overlapping detections", Order = 9)]
    public bool UseNonMaxSuppression { get; set; } = true;

    [Parameter(DisplayName = "Overlap Threshold", Description = "Maximum overlap ratio for non-maximum suppression (0.0-1.0)", Order = 10)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double OverlapThreshold { get; set; } = 0.3;

    [Parameter(DisplayName = "Draw Detections", Description = "Draw bounding boxes around detected patterns", Order = 11)]
    public bool DrawDetections { get; set; } = true;

    [Parameter(DisplayName = "Detection Color", Description = "Color of detection bounding boxes in hex format", Order = 12)]
    [ParameterColor]
    public string DetectionColorHex { get; set; } = "#00FF00";

    [Parameter(DisplayName = "Line Thickness", Description = "Thickness of bounding box lines (-1 = filled)", Order = 13)]
    [ParameterRange(-1, 10, 1)]
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Number of detections found in the last render
    /// </summary>
    public int DetectionCount { get; private set; }

    /// <summary>
    /// Training progress message
    /// </summary>
    public string TrainingStatus { get; private set; } = string.Empty;

    private StatModel? _model;
    private bool _isModelLoaded = false;

    public override string GetLayerType() => "PatternLearning";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            DetectionCount = 0;
            return new Mat();
        }

        if (Mode == PatternLearningMode.Training)
        {
            return HandleTraining(input);
        }
        else
        {
            return HandleDetection(input);
        }
    }

    private Mat HandleTraining(Mat input)
    {
        TrainingStatus = "Training not implemented via render. Use TrainModel() method.";
        return input.Clone();
    }

    private Mat HandleDetection(Mat input)
    {
        // Load model if not loaded
        if (!_isModelLoaded && !string.IsNullOrEmpty(ModelPath) && File.Exists(ModelPath))
        {
            LoadModel();
        }

        if (_model == null || !_isModelLoaded)
        {
            DetectionCount = 0;
            return input.Clone();
        }

        // Extract features from image using sliding window
        List<Rect> detections = new List<Rect>();
        List<double> confidences = new List<double>();

        Mat gray = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = input.Clone();
        }

        Size windowSize = new Size(WindowWidth, WindowHeight);

        // Sliding window detection
        for (int y = 0; y <= gray.Height - windowSize.Height; y += StepSize)
        {
            for (int x = 0; x <= gray.Width - windowSize.Width; x += StepSize)
            {
                Rect window = new Rect(x, y, windowSize.Width, windowSize.Height);
                Mat windowMat = new Mat(gray, window);

                // Extract features (simple HOG-like or histogram)
                Mat features = ExtractFeatures(windowMat);

                // Predict
                float prediction = _model.Predict(features);
                double confidence = Algorithm == PatternLearningAlgorithm.SVM 
                    ? Math.Abs(prediction) / 100.0  // Normalize SVM output
                    : prediction;  // KNN returns class directly

                // For SVM: positive class is typically 1, negative is -1
                // For KNN: positive class is 1, negative is 0
                bool isPositive = Algorithm == PatternLearningAlgorithm.SVM 
                    ? prediction > 0 
                    : prediction > 0.5;

                if (isPositive && confidence >= DetectionThreshold)
                {
                    detections.Add(window);
                    confidences.Add(Math.Min(1.0, confidence));
                }

                features.Dispose();
                windowMat.Dispose();
            }
        }

        gray.Dispose();

        // Apply non-maximum suppression
        if (UseNonMaxSuppression && detections.Count > 0)
        {
            var filtered = ApplyNonMaxSuppression(detections, confidences, OverlapThreshold);
            detections = filtered.rects;
            confidences = filtered.confidences;
        }

        DetectionCount = detections.Count;

        // Draw detections
        Mat result = input.Clone();
        if (DrawDetections)
        {
            var (b, g, r) = ColorUtils.HexToBgr(DetectionColorHex);
            var detectionColor = new Scalar(b, g, r);
            for (int i = 0; i < detections.Count; i++)
            {
                Cv2.Rectangle(result, detections[i], detectionColor, LineThickness);
            }
        }

        return result;
    }

    /// <summary>
    /// Trains the model using positive and negative samples
    /// </summary>
    public void TrainModel()
    {
        TrainingStatus = "Loading training samples...";

        // Load positive samples
        List<Mat> positiveSamples = LoadSamples(PositiveSamplesPath);
        if (positiveSamples.Count == 0)
        {
            TrainingStatus = "Error: No positive samples found";
            return;
        }

        // Load negative samples
        List<Mat> negativeSamples = LoadSamples(NegativeSamplesPath);
        if (negativeSamples.Count == 0)
        {
            TrainingStatus = "Error: No negative samples found";
            return;
        }

        TrainingStatus = $"Extracting features from {positiveSamples.Count} positive and {negativeSamples.Count} negative samples...";

        // Extract features
        List<Mat> allFeatures = new List<Mat>();
        List<float> allLabels = new List<float>();

        foreach (var sample in positiveSamples)
        {
            Mat features = ExtractFeatures(sample);
            allFeatures.Add(features);
            allLabels.Add(1.0f);  // Positive class
        }

        foreach (var sample in negativeSamples)
        {
            Mat features = ExtractFeatures(sample);
            allFeatures.Add(features);
            allLabels.Add(Algorithm == PatternLearningAlgorithm.SVM ? -1.0f : 0.0f);  // Negative class
        }

        // Combine features into training matrix
        int featureSize = allFeatures[0].Cols;
        Mat trainingData = new Mat(allFeatures.Count, featureSize, MatType.CV_32F);
        Mat labels = new Mat(allFeatures.Count, 1, MatType.CV_32S);

        for (int i = 0; i < allFeatures.Count; i++)
        {
            allFeatures[i].CopyTo(trainingData.Row(i));
            labels.Set<int>(i, 0, (int)allLabels[i]);
        }

        TrainingStatus = "Training model...";

        // Train model
        if (Algorithm == PatternLearningAlgorithm.SVM)
        {
            var svm = SVM.Create();
            svm.Type = SVM.Types.CSvc;
            svm.KernelType = SVM.KernelTypes.Rbf;
            svm.Gamma = 0.5;
            svm.C = 1.0;
            svm.TermCriteria = new TermCriteria(CriteriaTypes.MaxIter | CriteriaTypes.Eps, 1000, 1e-6);

            // Convert labels for SVM (1 for positive, -1 for negative)
            Mat svmLabels = new Mat(allFeatures.Count, 1, MatType.CV_32F);
            for (int i = 0; i < allLabels.Count; i++)
            {
                svmLabels.Set<float>(i, 0, allLabels[i]);
            }

            svm.Train(trainingData, SampleTypes.RowSample, svmLabels);
            _model = svm;
            svmLabels.Dispose();
        }
        else  // KNN
        {
            var knn = KNearest.Create();
            knn.DefaultK = 3;
            knn.Train(trainingData, SampleTypes.RowSample, labels);
            _model = knn;
        }

        _isModelLoaded = true;

        // Save model if path is specified
        if (!string.IsNullOrEmpty(ModelPath))
        {
            SaveModel();
        }

        // Cleanup
        foreach (var feature in allFeatures)
        {
            feature.Dispose();
        }
        foreach (var sample in positiveSamples)
        {
            sample.Dispose();
        }
        foreach (var sample in negativeSamples)
        {
            sample.Dispose();
        }
        trainingData.Dispose();
        labels.Dispose();

        TrainingStatus = $"Training complete! Model trained on {allFeatures.Count} samples.";
    }

    private List<Mat> LoadSamples(string directoryPath)
    {
        List<Mat> samples = new List<Mat>();

        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return samples;
        }

        string[] imageExtensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff", "*.tif", "*.gif" };
        foreach (var extension in imageExtensions)
        {
            string[] files = Directory.GetFiles(directoryPath, extension, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                Mat image = Cv2.ImRead(file, ImreadModes.Grayscale);
                if (!image.Empty())
                {
                    // Resize to window size if needed
                    Size windowSize = new Size(WindowWidth, WindowHeight);
                    if (image.Width != windowSize.Width || image.Height != windowSize.Height)
                    {
                        Mat resized = new Mat();
                        Cv2.Resize(image, resized, windowSize);
                        image.Dispose();
                        image = resized;
                    }
                    samples.Add(image);
                }
            }
        }

        return samples;
    }

    private Mat ExtractFeatures(Mat image)
    {
        // Simple feature extraction: Histogram of Oriented Gradients (HOG) simplified
        // For a more robust implementation, consider using OpenCV's HOGDescriptor

        Mat gray = image.Clone();
        if (gray.Channels() == 3)
        {
            Mat temp = gray;
            gray = new Mat();
            Cv2.CvtColor(temp, gray, ColorConversionCodes.BGR2GRAY);
            temp.Dispose();
        }

        // Calculate gradients
        Mat gradX = new Mat();
        Mat gradY = new Mat();
        Cv2.Sobel(gray, gradX, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_32F, 0, 1, 3);

        // Calculate magnitude and angle
        Mat magnitude = new Mat();
        Mat angle = new Mat();
        Cv2.CartToPolar(gradX, gradY, magnitude, angle, angleInDegrees: true);

        // Create histogram of oriented gradients (simplified: 9 bins)
        int bins = 9;
        float[] histogram = new float[bins];
        int cellSize = 8;
        int cellsX = gray.Width / cellSize;
        int cellsY = gray.Height / cellSize;

        List<float> features = new List<float>();

        for (int cy = 0; cy < cellsY; cy++)
        {
            for (int cx = 0; cx < cellsX; cx++)
            {
                Array.Clear(histogram, 0, bins);

                int startX = cx * cellSize;
                int startY = cy * cellSize;
                int endX = Math.Min(startX + cellSize, gray.Width);
                int endY = Math.Min(startY + cellSize, gray.Height);

                for (int y = startY; y < endY; y++)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        float mag = magnitude.Get<float>(y, x);
                        float ang = angle.Get<float>(y, x);

                        // Normalize angle to 0-180 degrees and bin it
                        int bin = (int)((ang % 180) / (180.0 / bins));
                        if (bin >= bins) bin = bins - 1;
                        histogram[bin] += mag;
                    }
                }

                // Normalize histogram
                float sum = histogram.Sum();
                if (sum > 0)
                {
                    for (int i = 0; i < bins; i++)
                    {
                        features.Add(histogram[i] / sum);
                    }
                }
                else
                {
                    for (int i = 0; i < bins; i++)
                    {
                        features.Add(0);
                    }
                }
            }
        }

        // Also add color histogram if color image
        if (image.Channels() == 3)
        {
            Mat[] channels = Cv2.Split(image);
            for (int c = 0; c < 3; c++)
            {
                int[] histSize = { 16 };
                Rangef[] ranges = { new Rangef(0, 256) };
                Mat hist = new Mat();
                Cv2.CalcHist(new[] { channels[c] }, new[] { 0 }, null, hist, 1, histSize, ranges);
                hist.ConvertTo(hist, MatType.CV_32F);
                hist /= hist.Sum().Val0;
                for (int i = 0; i < hist.Rows; i++)
                {
                    features.Add(hist.Get<float>(i, 0));
                }
                hist.Dispose();
                channels[c].Dispose();
            }
        }

        Mat featureVector = new Mat(1, features.Count, MatType.CV_32F);
        for (int i = 0; i < features.Count; i++)
        {
            featureVector.Set<float>(0, i, features[i]);
        }

        gray.Dispose();
        gradX.Dispose();
        gradY.Dispose();
        magnitude.Dispose();
        angle.Dispose();

        return featureVector;
    }

    private void SaveModel()
    {
        if (_model == null || string.IsNullOrEmpty(ModelPath))
        {
            return;
        }

        try
        {
            _model.Save(ModelPath);
            TrainingStatus = $"Model saved to {ModelPath}";
        }
        catch (Exception ex)
        {
            TrainingStatus = $"Error saving model: {ex.Message}";
        }
    }

    private void LoadModel()
    {
        if (string.IsNullOrEmpty(ModelPath) || !File.Exists(ModelPath))
        {
            _isModelLoaded = false;
            return;
        }

        try
        {
            if (Algorithm == PatternLearningAlgorithm.SVM)
            {
                _model = SVM.Load(ModelPath);
            }
            else
            {
                _model = KNearest.Load(ModelPath);
            }

            _isModelLoaded = _model != null;
            TrainingStatus = _isModelLoaded ? $"Model loaded from {ModelPath}" : "Failed to load model";
        }
        catch (Exception ex)
        {
            TrainingStatus = $"Error loading model: {ex.Message}";
            _isModelLoaded = false;
        }
    }

    private (List<Rect> rects, List<double> confidences) ApplyNonMaxSuppression(
        List<Rect> detections, List<double> confidences, double overlapThreshold)
    {
        if (detections.Count == 0)
            return (new List<Rect>(), new List<double>());

        // Create list with confidences
        var items = new List<(Rect rect, double confidence)>();
        for (int i = 0; i < detections.Count; i++)
        {
            items.Add((detections[i], confidences[i]));
        }

        // Sort by confidence (highest first)
        items.Sort((a, b) => b.confidence.CompareTo(a.confidence));

        // Apply non-maximum suppression
        List<Rect> filteredRects = new List<Rect>();
        List<double> filteredConfidences = new List<double>();

        while (items.Count > 0)
        {
            var best = items[0];
            filteredRects.Add(best.rect);
            filteredConfidences.Add(best.confidence);

            items.RemoveAt(0);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                double overlap = CalculateOverlap(best.rect, items[i].rect);
                if (overlap > overlapThreshold)
                {
                    items.RemoveAt(i);
                }
            }
        }

        return (filteredRects, filteredConfidences);
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
}

/// <summary>
/// Learning mode for pattern learning layer
/// </summary>
public enum PatternLearningMode
{
    /// <summary>
    /// Training mode - train model on samples
    /// </summary>
    Training,

    /// <summary>
    /// Detection mode - detect patterns using trained model
    /// </summary>
    Detection
}

/// <summary>
/// Machine learning algorithm for pattern learning
/// </summary>
public enum PatternLearningAlgorithm
{
    /// <summary>
    /// Support Vector Machine - good for binary classification
    /// </summary>
    SVM,

    /// <summary>
    /// K-Nearest Neighbors - simple, instance-based learning
    /// </summary>
    KNN
}

