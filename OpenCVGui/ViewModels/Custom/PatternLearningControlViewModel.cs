using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCVGui.Lib.Detection;
using OpenCVGui.Services;
using OpenCVGui.Utils;
using OpenCVGui.ViewModels;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Text;

namespace OpenCVGui.ViewModels.Custom;

public partial class PatternLearningControlViewModel : ViewModelBase
{
    private readonly LayerViewModel _layerViewModel;
    private PatternLearningLayer? _layer;
    private IFileDialogService? _fileDialogService;
    private readonly ImageViewportService _viewportService = new();
    private readonly CropService _cropService = new();
    private Mat? _sourceImage;
    private List<Mat> _sourceImages = new();
    private int _currentImageIndex = -1;

    [ObservableProperty]
    private int _positiveSampleCount;

    [ObservableProperty]
    private int _negativeSampleCount;

    [ObservableProperty]
    private int _testSampleCount;

    [ObservableProperty]
    private bool _isTraining;

    [ObservableProperty]
    private string _trainingStatus = string.Empty;

    [ObservableProperty]
    private Bitmap? _displayImage;

    [ObservableProperty]
    private bool _isCropMode;

    [ObservableProperty]
    private ImageCropData? _currentCropData;

    [ObservableProperty]
    private double _rotationAngle = 0.0;

    [ObservableProperty]
    private bool _canNavigatePrevious;

    [ObservableProperty]
    private bool _canNavigateNext;

    [ObservableProperty]
    private string _imageNavigationInfo = string.Empty;

    public double ZoomLevel
    {
        get => _viewportService.ZoomLevel;
        set
        {
            _viewportService.ZoomLevel = value;
            OnPropertyChanged();
        }
    }

    public double PanX
    {
        get => _viewportService.PanX;
        set
        {
            _viewportService.PanX = value;
            OnPropertyChanged();
        }
    }

    public double PanY
    {
        get => _viewportService.PanY;
        set
        {
            _viewportService.PanY = value;
            OnPropertyChanged();
        }
    }

    public PatternLearningControlViewModel(LayerViewModel layerViewModel)
    {
        _layerViewModel = layerViewModel ?? throw new ArgumentNullException(nameof(layerViewModel));
        _layer = layerViewModel.Layer as PatternLearningLayer;
        
        if (_layer == null)
        {
            throw new ArgumentException("Layer must be a PatternLearningLayer", nameof(layerViewModel));
        }

        RefreshSampleCounts();
        UpdateTrainingStatus();
    }

    public void SetFileDialogService(IFileDialogService fileDialogService)
    {
        _fileDialogService = fileDialogService;
    }

    public string PositiveSamplesPath
    {
        get => _layer?.PositiveSamplesPath ?? string.Empty;
        set
        {
            if (_layer != null && _layer.PositiveSamplesPath != value)
            {
                _layer.PositiveSamplesPath = value;
                OnPropertyChanged();
                RefreshSampleCounts();
            }
        }
    }

    public string NegativeSamplesPath
    {
        get => _layer?.NegativeSamplesPath ?? string.Empty;
        set
        {
            if (_layer != null && _layer.NegativeSamplesPath != value)
            {
                _layer.NegativeSamplesPath = value;
                OnPropertyChanged();
                RefreshSampleCounts();
            }
        }
    }

    public string TestSamplesPath
    {
        get => _layer?.TestSamplesPath ?? string.Empty;
        set
        {
            if (_layer != null && _layer.TestSamplesPath != value)
            {
                _layer.TestSamplesPath = value;
                OnPropertyChanged();
                RefreshSampleCounts();
            }
        }
    }

    private void UpdateTrainingStatus()
    {
        if (_layer != null)
        {
            TrainingStatus = _layer.TrainingStatus;
        }
    }

    private void RefreshSampleCounts()
    {
        // Count base64 samples first (primary storage)
        int positiveBase64 = _layer?.PositiveSamplesBase64?.Count ?? 0;
        int negativeBase64 = _layer?.NegativeSamplesBase64?.Count ?? 0;
        int testBase64 = _layer?.TestSamplesBase64?.Count ?? 0;

        // Also count directory samples (for backward compatibility)
        int positiveDir = CountSamples(PositiveSamplesPath);
        int negativeDir = CountSamples(NegativeSamplesPath);
        int testDir = CountSamples(TestSamplesPath);

        PositiveSampleCount = positiveBase64 + positiveDir;
        NegativeSampleCount = negativeBase64 + negativeDir;
        TestSampleCount = testBase64 + testDir;

        OnPropertyChanged(nameof(PositiveSampleThumbnails));
        OnPropertyChanged(nameof(NegativeSampleThumbnails));
        OnPropertyChanged(nameof(TestSampleThumbnails));
    }

    private int CountSamples(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        string[] imageExtensions = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff", "*.tif", "*.gif" };
        int count = 0;
        foreach (var extension in imageExtensions)
        {
            count += Directory.GetFiles(directoryPath, extension, SearchOption.TopDirectoryOnly).Length;
        }
        return count;
    }

    [RelayCommand]
    private async Task BrowsePositiveSamplesPathAsync()
    {
        if (_fileDialogService == null)
            return;

        var topLevel = _fileDialogService.GetTopLevel();
        if (topLevel == null)
            return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Positive Samples Directory"
        });

        if (folder.Count > 0 && folder[0].TryGetLocalPath() is string path)
        {
            PositiveSamplesPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseNegativeSamplesPathAsync()
    {
        if (_fileDialogService == null)
            return;

        var topLevel = _fileDialogService.GetTopLevel();
        if (topLevel == null)
            return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Negative Samples Directory"
        });

        if (folder.Count > 0 && folder[0].TryGetLocalPath() is string path)
        {
            NegativeSamplesPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseTestSamplesPathAsync()
    {
        if (_fileDialogService == null)
            return;

        var topLevel = _fileDialogService.GetTopLevel();
        if (topLevel == null)
            return;

        var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Test Samples Directory"
        });

        if (folder.Count > 0 && folder[0].TryGetLocalPath() is string path)
        {
            TestSamplesPath = path;
        }
    }

    [RelayCommand]
    private async Task ImportImageAsync()
    {
        if (_fileDialogService == null)
            return;

        var files = await _fileDialogService.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Images to Import",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tiff", "*.tif", "*.gif" }
                }
            },
            AllowMultiple = true
        });

        if (files != null && files.Count > 0)
        {
            // Dispose existing images
            foreach (var img in _sourceImages)
            {
                img?.Dispose();
            }
            _sourceImages.Clear();

            // Load all selected images
            foreach (var file in files)
            {
                if (file.TryGetLocalPath() is string filePath)
                {
                    var image = Cv2.ImRead(filePath, ImreadModes.Color);
                    if (!image.Empty())
                    {
                        _sourceImages.Add(image);
                    }
                }
            }

            if (_sourceImages.Count > 0)
            {
                _currentImageIndex = 0;
                SetSourceImage(_sourceImages[0]);
                UpdateNavigationState();
            }
        }
    }

    public void SetSourceImage(Mat image)
    {
        _sourceImage = image;
        RotationAngle = 0.0;
        UpdateDisplayImage();
        FitToView();
    }

    partial void OnPositiveSampleCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanTrain));
        TrainModelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(PositiveSampleThumbnails));
    }

    partial void OnNegativeSampleCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanTrain));
        TrainModelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(NegativeSampleThumbnails));
    }

    partial void OnTestSampleCountChanged(int value)
    {
        OnPropertyChanged(nameof(TestSampleThumbnails));
    }

    [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
    private void NavigatePrevious()
    {
        if (_currentImageIndex > 0)
        {
            _currentImageIndex--;
            SetSourceImage(_sourceImages[_currentImageIndex]);
            UpdateNavigationState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateNext))]
    private void NavigateNext()
    {
        if (_currentImageIndex < _sourceImages.Count - 1)
        {
            _currentImageIndex++;
            SetSourceImage(_sourceImages[_currentImageIndex]);
            UpdateNavigationState();
        }
    }

    private void UpdateNavigationState()
    {
        CanNavigatePrevious = _currentImageIndex > 0;
        CanNavigateNext = _currentImageIndex < _sourceImages.Count - 1;
        ImageNavigationInfo = _sourceImages.Count > 0 
            ? $"Image {_currentImageIndex + 1} of {_sourceImages.Count}"
            : string.Empty;
        NavigatePreviousCommand.NotifyCanExecuteChanged();
        NavigateNextCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDisplayImage()
    {
        if (_sourceImage != null && !_sourceImage.Empty())
        {
            Mat displayMat = ApplyRotation(_sourceImage, RotationAngle);
            DisplayImage = ImageConverter.MatToBitmapSafe(displayMat);
            if (displayMat != _sourceImage)
            {
                displayMat.Dispose();
            }
        }
        else
        {
            DisplayImage = null;
        }
    }

    private Mat ApplyRotation(Mat source, double angle)
    {
        if (source == null || source.Empty() || Math.Abs(angle % 360.0) < 0.01)
        {
            return source ?? new Mat();
        }

        var center = new Point2f(source.Width / 2.0f, source.Height / 2.0f);
        var rotationMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);

        // Calculate bounding box of rotated image
        var corners = new Point2f[]
        {
            new(0, 0),
            new(source.Width, 0),
            new(source.Width, source.Height),
            new(0, source.Height)
        };

        var rotatedCorners = new List<Point2f>();
        var angleRad = angle * Math.PI / 180.0;
        var cos = Math.Cos(angleRad);
        var sin = Math.Sin(angleRad);

        foreach (var corner in corners)
        {
            var x = corner.X - center.X;
            var y = corner.Y - center.Y;
            var rotatedX = x * cos - y * sin;
            var rotatedY = x * sin + y * cos;
            rotatedCorners.Add(new Point2f((float)(rotatedX + center.X), (float)(rotatedY + center.Y)));
        }

        var minX = rotatedCorners.Min(c => c.X);
        var maxX = rotatedCorners.Max(c => c.X);
        var minY = rotatedCorners.Min(c => c.Y);
        var maxY = rotatedCorners.Max(c => c.Y);

        var outputSize = new Size((int)Math.Ceiling(maxX - minX), (int)Math.Ceiling(maxY - minY));

        // Adjust translation
        var tx = rotationMatrix.Get<double>(0, 2);
        var ty = rotationMatrix.Get<double>(1, 2);
        rotationMatrix.Set<double>(0, 2, tx - minX);
        rotationMatrix.Set<double>(1, 2, ty - minY);

        var result = new Mat();
        Cv2.WarpAffine(source, result, rotationMatrix, outputSize, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        rotationMatrix.Dispose();

        return result;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        _viewportService.ZoomIn();
        OnPropertyChanged(nameof(ZoomLevel));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        _viewportService.ZoomOut();
        OnPropertyChanged(nameof(ZoomLevel));
    }

    [RelayCommand]
    private void FitToView()
    {
        if (DisplayImage == null)
            return;

        _viewportService.FitToView();
        OnPropertyChanged(nameof(ZoomLevel));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    [RelayCommand]
    private void RotateLeft()
    {
        RotationAngle = (RotationAngle - 5.0) % 360.0;
        if (RotationAngle < 0) RotationAngle += 360.0;
        UpdateDisplayImage();
        FitToView();
    }

    [RelayCommand]
    private void RotateRight()
    {
        RotationAngle = (RotationAngle + 5.0) % 360.0;
        UpdateDisplayImage();
        FitToView();
    }

    partial void OnRotationAngleChanged(double value)
    {
        UpdateDisplayImage();
    }

    public void ZoomAtPoint(double delta, double x, double y)
    {
        if (DisplayImage == null)
            return;

        var oldZoom = ZoomLevel;
        _viewportService.ZoomAtPoint(delta, x, y, DisplayImage);

        if (Math.Abs(ZoomLevel - oldZoom) > 0.001)
        {
            OnPropertyChanged(nameof(ZoomLevel));
            OnPropertyChanged(nameof(PanX));
            OnPropertyChanged(nameof(PanY));
        }
    }

    public void PanImage(double deltaX, double deltaY)
    {
        _viewportService.PanImage(deltaX, deltaY);
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    public void SetCropSelection(int x, int y, int width, int height)
    {
        if (!IsCropMode || _sourceImage == null || _sourceImage.Empty())
            return;

        try
        {
            CurrentCropData?.Dispose();
            
            // Apply rotation to source image before cropping
            Mat rotatedSource = ApplyRotation(_sourceImage, RotationAngle);
            bool needsDispose = rotatedSource != _sourceImage;
            
            try
            {
                CurrentCropData = _cropService.CreateCropData(rotatedSource, x, y, width, height);
                OnCropDataChanged();
            }
            finally
            {
                if (needsDispose)
                {
                    rotatedSource.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting crop selection: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StartCropMode()
    {
        IsCropMode = true;
        CurrentCropData = null;
    }

    [RelayCommand]
    private void CancelCrop()
    {
        IsCropMode = false;
        CurrentCropData?.Dispose();
        CurrentCropData = null;
        OnCropDataChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSaveCropAsPositive))]
    private void SaveCropAsPositive()
    {
        if (_layer == null || CurrentCropData == null)
            return;

        SaveCropToDirectory(CurrentCropData, PositiveSamplesPath, SampleCategory.Positive);
        CancelCrop();
        RefreshSampleCounts();
    }

    [RelayCommand(CanExecute = nameof(CanSaveCropAsNegative))]
    private void SaveCropAsNegative()
    {
        if (_layer == null || CurrentCropData == null)
            return;

        SaveCropToDirectory(CurrentCropData, NegativeSamplesPath, SampleCategory.Negative);
        CancelCrop();
        RefreshSampleCounts();
    }

    [RelayCommand(CanExecute = nameof(CanSaveCropAsTest))]
    private void SaveCropAsTest()
    {
        if (_layer == null || CurrentCropData == null)
            return;

        SaveCropToDirectory(CurrentCropData, TestSamplesPath, SampleCategory.Test);
        CancelCrop();
        RefreshSampleCounts();
    }

    private enum SampleCategory
    {
        Positive,
        Negative,
        Test
    }

    private void SaveCropToDirectory(ImageCropData cropData, string directoryPath, SampleCategory category)
    {
        if (_layer == null)
            return;

        try
        {
            Mat cropped = cropData.CroppedImage.Clone();

            Size targetSize = new Size(_layer.WindowWidth, _layer.WindowHeight);
            if (cropped.Width != targetSize.Width || cropped.Height != targetSize.Height)
            {
                Mat resized = new Mat();
                Cv2.Resize(cropped, resized, targetSize);
                cropped.Dispose();
                cropped = resized;
            }

            // Encode to PNG and convert to base64
            byte[] imageBytes = cropped.ImEncode(".png");
            string base64String = Convert.ToBase64String(imageBytes);

            // Add to appropriate base64 list
            switch (category)
            {
                case SampleCategory.Positive:
                    _layer.PositiveSamplesBase64.Add(base64String);
                    break;
                case SampleCategory.Negative:
                    _layer.NegativeSamplesBase64.Add(base64String);
                    break;
                case SampleCategory.Test:
                    _layer.TestSamplesBase64.Add(base64String);
                    break;
            }

            // Optionally also save to directory if path is provided (for backward compatibility)
            if (!string.IsNullOrEmpty(directoryPath))
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var categoryName = category switch
                {
                    SampleCategory.Positive => "positive",
                    SampleCategory.Negative => "negative",
                    SampleCategory.Test => "test",
                    _ => "unknown"
                };
                var filename = $"{categoryName}_{timestamp}.png";
                var filePath = Path.Combine(directoryPath, filename);
                Cv2.ImWrite(filePath, cropped);
            }

            cropped.Dispose();
            
            // Refresh thumbnails
            OnPropertyChanged(nameof(PositiveSampleThumbnails));
            OnPropertyChanged(nameof(NegativeSampleThumbnails));
            OnPropertyChanged(nameof(TestSampleThumbnails));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving crop: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanTrain))]
    private void TrainModel()
    {
        if (_layer == null || IsTraining)
            return;

        if (PositiveSampleCount == 0)
        {
            TrainingStatus = "Error: No positive samples found";
            return;
        }

        if (NegativeSampleCount == 0)
        {
            TrainingStatus = "Error: No negative samples found";
            return;
        }

        IsTraining = true;
        TrainingStatus = "Training in progress...";
        TrainModelCommand.NotifyCanExecuteChanged();

        try
        {
            _layer.TrainModel();
            UpdateTrainingStatus();
        }
        catch (Exception ex)
        {
            TrainingStatus = $"Training error: {ex.Message}";
        }
        finally
        {
            IsTraining = false;
            TrainModelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanSaveCropAsPositive => CurrentCropData != null && IsCropMode;
    public bool CanSaveCropAsNegative => CurrentCropData != null && IsCropMode;
    public bool CanSaveCropAsTest => CurrentCropData != null && IsCropMode;
    public bool CanTrain => !IsTraining && PositiveSampleCount > 0 && NegativeSampleCount > 0;

    public void OnCropDataChanged()
    {
        OnPropertyChanged(nameof(CanSaveCropAsPositive));
        OnPropertyChanged(nameof(CanSaveCropAsNegative));
        OnPropertyChanged(nameof(CanSaveCropAsTest));
        SaveCropAsPositiveCommand.NotifyCanExecuteChanged();
        SaveCropAsNegativeCommand.NotifyCanExecuteChanged();
        SaveCropAsTestCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCropModeChanged(bool value)
    {
        if (!value)
        {
            CancelCrop();
        }
        OnCropDataChanged();
    }

    partial void OnIsTrainingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTrain));
        TrainModelCommand.NotifyCanExecuteChanged();
    }


    public List<Bitmap> PositiveSampleThumbnails
    {
        get
        {
            var thumbnails = new List<Bitmap>();
            if (_layer?.PositiveSamplesBase64 != null)
            {
                foreach (var base64 in _layer.PositiveSamplesBase64)
                {
                    try
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        Mat image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                        if (!image.Empty())
                        {
                            var thumbnail = CreateThumbnail(image, 100);
                            thumbnails.Add(thumbnail);
                            image.Dispose();
                        }
                    }
                    catch
                    {
                        // Skip invalid base64 strings
                    }
                }
            }
            return thumbnails;
        }
    }

    public List<Bitmap> NegativeSampleThumbnails
    {
        get
        {
            var thumbnails = new List<Bitmap>();
            if (_layer?.NegativeSamplesBase64 != null)
            {
                foreach (var base64 in _layer.NegativeSamplesBase64)
                {
                    try
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        Mat image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                        if (!image.Empty())
                        {
                            var thumbnail = CreateThumbnail(image, 100);
                            thumbnails.Add(thumbnail);
                            image.Dispose();
                        }
                    }
                    catch
                    {
                        // Skip invalid base64 strings
                    }
                }
            }
            return thumbnails;
        }
    }

    public List<Bitmap> TestSampleThumbnails
    {
        get
        {
            var thumbnails = new List<Bitmap>();
            if (_layer?.TestSamplesBase64 != null)
            {
                foreach (var base64 in _layer.TestSamplesBase64)
                {
                    try
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        Mat image = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                        if (!image.Empty())
                        {
                            var thumbnail = CreateThumbnail(image, 100);
                            thumbnails.Add(thumbnail);
                            image.Dispose();
                        }
                    }
                    catch
                    {
                        // Skip invalid base64 strings
                    }
                }
            }
            return thumbnails;
        }
    }

    private Bitmap CreateThumbnail(Mat image, int maxSize)
    {
        double scale = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
        int newWidth = (int)(image.Width * scale);
        int newHeight = (int)(image.Height * scale);

        Mat resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight));
        var bitmap = ImageConverter.MatToBitmapSafe(resized);
        resized.Dispose();
        return bitmap;
    }
}
