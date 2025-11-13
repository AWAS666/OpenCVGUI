using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCVGui.Lib;
using OpenCVGui.Lib.Serialization;
using OpenCVGui.Utils;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace OpenCVGui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LayerChain _layerChain = new();
    private Mat? _sourceImage;
    private CancellationTokenSource? _streamingCancellationTokenSource;
    private Task? _streamingTask;
    private readonly object _streamingLock = new object();

    [ObservableProperty]
    private ObservableCollection<LayerViewModel> _layers = new();

    [ObservableProperty]
    private LayerViewModel? _selectedLayer;

    [ObservableProperty]
    private Bitmap? _displayImage;

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private bool _hasFileParameter;

    [ObservableProperty]
    private RenderMode _renderMode = RenderMode.UI;

    [ObservableProperty]
    private bool _isCropMode = false;

    [ObservableProperty]
    private ImageCropData? _currentCropData;

    [ObservableProperty]
    private ParameterProperty? _targetCropParameter;

    [ObservableProperty]
    private ObservableCollection<string> _errorLog = new();

    [ObservableProperty]
    private bool _hasErrors;

    public bool IsUIRenderMode => RenderMode == RenderMode.UI;
    
    public bool IsChainRenderMode => RenderMode == RenderMode.Chain;

    public IBrush UIRenderModeIconColor => IsUIRenderMode ? Brushes.Green : Brushes.Gray;
    
    public IBrush ChainRenderModeIconColor => IsChainRenderMode ? Brushes.Blue : Brushes.Gray;
    
    /// <summary>
    /// Total runtime of all layers in milliseconds
    /// </summary>
    public double TotalRuntime
    {
        get
        {
            return Layers.Sum(layer => layer.LastRuntime);
        }
    }
    
    [ObservableProperty]
    private double _zoomLevel = 1.0;
    
    [ObservableProperty]
    private double _panX = 0.0;
    
    [ObservableProperty]
    private double _panY = 0.0;
    
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 0.1;

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(MaxZoom, ZoomLevel + ZoomStep);
    }
    
    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(MinZoom, ZoomLevel - ZoomStep);
    }
    
    [RelayCommand]
    private void FitToView()
    {
        if (DisplayImage == null)
            return;
            
        // Reset zoom and pan to fit image
        ZoomLevel = 1.0;
        PanX = 0.0;
        PanY = 0.0;
        OnPropertyChanged(nameof(ZoomLevel));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }
    
    partial void OnDisplayImageChanged(Bitmap? value)
    {
        // Don't reset zoom on image change - preserve user's zoom level
        // Only reset if this is the first image (zoom is still at default 1.0)
        if (value != null && ZoomLevel == 1.0 && PanX == 0.0 && PanY == 0.0)
        {
            // This is likely the first image, so fit to view is fine
            // But don't do it automatically - let user control it
        }
    }
    
    public void ZoomAtPoint(double delta, double x, double y)
    {
        if (DisplayImage == null)
            return;
            
        var oldZoom = ZoomLevel;
        var zoomDelta = delta * ZoomStep * 0.5; // Make wheel zoom smoother
        var newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, ZoomLevel + zoomDelta));
        
        if (Math.Abs(newZoom - oldZoom) < 0.001)
            return;
            
        // x and y are relative to the image control (before transform)
        // We need to account for the current zoom and pan to find the point in image space
        var imageWidth = DisplayImage.PixelSize.Width;
        var imageHeight = DisplayImage.PixelSize.Height;
        
        // Convert mouse position from image control coordinates to image space
        // Image control is centered, so we need to account for that
        var imageSpaceX = x - imageWidth / 2.0;
        var imageSpaceY = y - imageHeight / 2.0;
        
        // Calculate zoom factor
        var zoomFactor = newZoom / oldZoom;
        
        // Adjust pan to keep the point under the mouse cursor fixed
        PanX = imageSpaceX - (imageSpaceX - PanX) * zoomFactor;
        PanY = imageSpaceY - (imageSpaceY - PanY) * zoomFactor;
        
        ZoomLevel = newZoom;
        OnPropertyChanged(nameof(ZoomLevel));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }
    
    public void PanImage(double deltaX, double deltaY)
    {
        PanX += deltaX;
        PanY += deltaY;
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    [ObservableProperty]
    private bool _isStreamingPaused = false;

    public bool IsStreamingPlaying => !IsStreamingPaused;
    
    public bool IsStreamingActive => IsStreamingActiveInternal();
    
    public string PauseButtonIcon => IsStreamingPaused ? "Play" : "Pause";
    
    public string PauseButtonTooltip => IsStreamingPaused ? "Resume Streaming" : "Pause Streaming";

    partial void OnRenderModeChanged(RenderMode value)
    {
        OnPropertyChanged(nameof(IsUIRenderMode));
        OnPropertyChanged(nameof(IsChainRenderMode));
        OnPropertyChanged(nameof(UIRenderModeIconColor));
        OnPropertyChanged(nameof(ChainRenderModeIconColor));
        RenderChain();
    }
    
    partial void OnIsStreamingPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStreamingPlaying));
        OnPropertyChanged(nameof(PauseButtonIcon));
        OnPropertyChanged(nameof(PauseButtonTooltip));
        OnPropertyChanged(nameof(CanStepFrames));
        StepFrameForwardCommand.NotifyCanExecuteChanged();
        StepFrameBackwardCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCropModeChanged(bool value)
    {
        if (!value)
        {
            // Clean up when exiting crop mode
            CancelCrop();
        }
    }

    [RelayCommand]
    private void SetRenderMode(string mode)
    {
        if (Enum.TryParse<RenderMode>(mode, out var renderMode))
        {
            RenderMode = renderMode;
        }
    }

    [RelayCommand]
    private void StartCrop(ParameterProperty? parameter)
    {
        if (parameter == null)
        {
            System.Diagnostics.Debug.WriteLine("StartCrop: parameter is null");
            return;
        }

        if (DisplayImage == null)
        {
            System.Diagnostics.Debug.WriteLine("StartCrop: DisplayImage is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"StartCrop: Starting crop mode for parameter {parameter.PropertyName}");
        TargetCropParameter = parameter;
        IsCropMode = true;
        CurrentCropData = null;
        OnPropertyChanged(nameof(IsCropMode));
    }

    [RelayCommand]
    private void ApplyCrop()
    {
        if (TargetCropParameter == null || CurrentCropData == null)
            return;

        try
        {
            // Store the cropped image as a Mat
            // For now, we'll store it directly if the property type is Mat
            // Otherwise, we might need to serialize it
            if (TargetCropParameter.PropertyType == typeof(Mat))
            {
                TargetCropParameter.Value = CurrentCropData.CroppedImage.Clone();
            }
            else if (TargetCropParameter.PropertyType == typeof(byte[]))
            {
                // Convert Mat to byte array (PNG format)
                var encoded = CurrentCropData.CroppedImage.ImEncode(".png");
                TargetCropParameter.Value = encoded;
            }
            else if (TargetCropParameter.PropertyType == typeof(string))
            {
                // Save to temp file and store path
                var tempPath = Path.Combine(Path.GetTempPath(), $"crop_{Guid.NewGuid()}.png");
                Cv2.ImWrite(tempPath, CurrentCropData.CroppedImage);
                TargetCropParameter.Value = tempPath;
            }

            CancelCrop();
            OnPropertyChanged(nameof(IsCropMode));
            RenderChain();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying crop: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CancelCrop()
    {
        IsCropMode = false;
        TargetCropParameter = null;
        CurrentCropData?.Dispose();
        CurrentCropData = null;
        OnPropertyChanged(nameof(IsCropMode));
    }

    public void SetCropSelection(int x, int y, int width, int height)
    {
        if (!IsCropMode || DisplayImage == null)
            return;

        try
        {
            // Get the current rendered image Mat
            Mat? sourceMat = null;
            
            if (SelectedLayer != null)
            {
                var selectedIndex = Layers.IndexOf(SelectedLayer);
                if (selectedIndex >= 0)
                {
                    sourceMat = _layerChain.RenderUpTo(selectedIndex, _sourceImage, RenderMode.Chain);
                }
            }
            else if (_sourceImage != null && !_sourceImage.Empty())
            {
                sourceMat = _sourceImage.Clone();
            }
            else if (_layerChain.Count > 0)
            {
                sourceMat = _layerChain.RenderChain(_sourceImage, RenderMode.Chain);
            }

            if (sourceMat == null || sourceMat.Empty())
            {
                sourceMat?.Dispose();
                return;
            }

            // Ensure coordinates are within image bounds
            var imgWidth = sourceMat.Width;
            var imgHeight = sourceMat.Height;
            
            x = Math.Max(0, Math.Min(x, imgWidth - 1));
            y = Math.Max(0, Math.Min(y, imgHeight - 1));
            width = Math.Max(1, Math.Min(width, imgWidth - x));
            height = Math.Max(1, Math.Min(height, imgHeight - y));

            // Extract the cropped region
            var rect = new Rect(x, y, width, height);
            var cropped = new Mat(sourceMat, rect);
            
            // Dispose old crop data if exists
            CurrentCropData?.Dispose();
            
            CurrentCropData = new ImageCropData(cropped.Clone(), x, y, width, height);
            
            // Clean up temporary source mat
            if (sourceMat != _sourceImage)
            {
                sourceMat.Dispose();
            }
            
            OnPropertyChanged(nameof(CurrentCropData));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting crop selection: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleStreamingPause()
    {
        lock (_streamingLock)
        {
            IsStreamingPaused = !IsStreamingPaused;
        }
        OnPropertyChanged(nameof(IsStreamingPlaying));
        OnPropertyChanged(nameof(PauseButtonIcon));
        OnPropertyChanged(nameof(PauseButtonTooltip));
        OnPropertyChanged(nameof(IsStreamingActive));
    }
    
    public bool CanStepFrames => IsStreamingActive && IsStreamingPaused;
    
    [RelayCommand(CanExecute = nameof(CanStepFrames))]
    private void StepFrameForward()
    {
        if (!IsStreamingActive || !IsStreamingPaused)
            return;
            
        var videoLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.VideoLayer>()
            .FirstOrDefault(l => l.IsEnabled && l.Streaming);
            
        if (videoLayer != null)
        {
            videoLayer.StepForward();
        }
        
        // Trigger a single frame render
        RenderChain();
    }
    
    [RelayCommand(CanExecute = nameof(CanStepFrames))]
    private void StepFrameBackward()
    {
        if (!IsStreamingActive || !IsStreamingPaused)
            return;
            
        var videoLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.VideoLayer>()
            .FirstOrDefault(l => l.IsEnabled && l.Streaming);
            
        if (videoLayer != null)
        {
            videoLayer.StepBackward();
        }
        
        // Trigger a single frame render
        RenderChain();
    }
    
    private bool IsStreamingActiveInternal()
    {
        return _streamingTask != null && !_streamingTask.IsCompleted;
    }

    partial void OnSelectedLayerChanged(LayerViewModel? value)
    {
        UpdateFileParameterVisibility();
        RenderChain();
        RemoveLayerCommand.NotifyCanExecuteChanged();
        ResetLayerToDefaultsCommand.NotifyCanExecuteChanged();
    }

    private void UpdateFileParameterVisibility()
    {
        HasFileParameter = SelectedLayer?.Parameters.Any(p => p.GetFileAttribute() != null) ?? false;
    }

    [RelayCommand]
    private async Task LoadImageAsync()
    {
        if (SelectedLayer == null)
            return;

        // Find the file parameter
        var fileParam = SelectedLayer.Parameters.FirstOrDefault(p => p.GetFileAttribute() != null);
        if (fileParam == null)
            return;

        var topLevel = GetTopLevel();
        if (topLevel == null)
            return;

        var fileAttr = fileParam.GetFileAttribute();
        var fileTypeFilter = new List<FilePickerFileType>();
        
        if (!string.IsNullOrEmpty(fileAttr?.Filter))
        {
            // Parse filter string like "Image files|*.png;*.jpg"
            var parts = fileAttr.Filter.Split('|');
            if (parts.Length == 2)
            {
                var patterns = parts[1].Split(';');
                fileTypeFilter.Add(new FilePickerFileType(parts[0])
                {
                    Patterns = patterns
                });
            }
        }
        
        if (fileTypeFilter.Count == 0)
        {
            fileTypeFilter.Add(FilePickerFileTypes.ImageAll);
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Image",
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilter
        });

        if (files.Count > 0 && files[0].Path.IsFile)
        {
            var filePath = files[0].Path.LocalPath;
            
            // Set the file path parameter
            fileParam.Value = filePath;
            
            // Also load the image for direct display if it's an ImageImportLayer
            if (SelectedLayer.Layer is OpenCVGui.Lib.Import.ImageImportLayer)
            {
                _sourceImage = Cv2.ImRead(filePath, ImreadModes.Color);
                if (!_sourceImage.Empty())
                {
                    RenderChain();
                }
            }
            else
            {
                RenderChain();
            }
        }
    }

    [RelayCommand]
    private async Task ExportImageAsync()
    {
        if (DisplayImage == null)
            return;

        var topLevel = GetTopLevel();
        if (topLevel == null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Image",
            DefaultExtension = "png",
            SuggestedFileName = "exported_image.png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG Image")
                {
                    Patterns = new[] { "*.png" }
                },
                new FilePickerFileType("JPEG Image")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg" }
                },
                new FilePickerFileType("BMP Image")
                {
                    Patterns = new[] { "*.bmp" }
                }
            }
        });

        if (file != null && file.Path.IsFile)
        {
            try
            {
                // Get the current rendered result
                Mat? result = null;
                if (SelectedLayer != null)
                {
                    var selectedIndex = Layers.IndexOf(SelectedLayer);
                    if (selectedIndex >= 0)
                    {
                        result = _layerChain.RenderUpTo(selectedIndex, _sourceImage, RenderMode.Chain);
                    }
                }
                else if (_sourceImage != null)
                {
                    result = _sourceImage.Clone();
                }
                else if (_layerChain.Count > 0)
                {
                    result = _layerChain.RenderChain(_sourceImage, RenderMode.Chain);
                }

                if (result != null && !result.Empty())
                {
                    var filePath = file.Path.LocalPath;
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();
                    
                    if (extension == ".jpg" || extension == ".jpeg")
                    {
                        Cv2.ImWrite(filePath, result, new int[] { (int)ImwriteFlags.JpegQuality, 95 });
                    }
                    else
                    {
                        // PNG, BMP, and other formats
                        Cv2.ImWrite(filePath, result);
                    }
                    result.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting image: {ex.Message}");
            }
        }
    }

    [ObservableProperty]
    private LayerTypeItem? _selectedLayerType;

    [ObservableProperty]
    private ObservableCollection<LayerTypeItem> _availableLayerTypes = new();

    partial void OnSelectedLayerTypeChanged(LayerTypeItem? value)
    {
        if (value != null)
        {
            AddLayerOfType(value.LayerTypeId);
        }
        AddSelectedLayerCommand.NotifyCanExecuteChanged();
    }

    public bool CanAddSelectedLayer => SelectedLayerType != null;

    [RelayCommand(CanExecute = nameof(CanAddSelectedLayer))]
    private void AddSelectedLayer()
    {
        if (SelectedLayerType != null)
        {
            AddLayerOfType(SelectedLayerType.LayerTypeId);
        }
    }

    public MainWindowViewModel()
    {
        // Initialize layer factory
        LayerFactory.Initialize();
        
        // Populate available layer types
        var layerTypes = LayerFactory.GetAllLayerTypesWithNames();
        foreach (var kvp in layerTypes.OrderBy(x => x.Value))
        {
            AvailableLayerTypes.Add(new LayerTypeItem
            {
                LayerTypeId = kvp.Key,
                DisplayName = kvp.Value
            });
        }
    }

    private void AddLayerOfType(string layerType)
    {
        try
        {
            var layer = LayerFactory.CreateLayer(layerType);
            var displayName = LayerFactory.GetDisplayName(layerType);
            layer.Name = displayName;
            AddLayerToChain(layer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating layer: {ex.Message}");
        }
    }

    public bool CanRemoveLayer => SelectedLayer != null;

    [RelayCommand(CanExecute = nameof(CanRemoveLayer))]
    private void RemoveLayer()
    {
        if (SelectedLayer != null)
        {
            var index = Layers.IndexOf(SelectedLayer);
            if (index >= 0)
            {
                _layerChain.RemoveLayerAt(index);
                Layers.RemoveAt(index);
                
                if (Layers.Count > 0)
                {
                    SelectedLayer = index < Layers.Count ? Layers[index] : Layers[Layers.Count - 1];
                }
                else
                {
                    SelectedLayer = null;
                }
                
                CheckAndUpdateStreaming();
                
                if (!IsStreamingActive)
                {
                    RenderChain();
                }
                
                RemoveLayerCommand.NotifyCanExecuteChanged();
            }
        }
    }


    [RelayCommand]
    private void MoveLayerUp()
    {
        if (SelectedLayer != null)
        {
            var index = Layers.IndexOf(SelectedLayer);
            if (index > 0)
            {
                _layerChain.MoveLayer(index, index - 1);
                Layers.Move(index, index - 1);
                RenderChain();
            }
        }
    }

    public bool CanResetLayerToDefaults => SelectedLayer != null;

    [RelayCommand(CanExecute = nameof(CanResetLayerToDefaults))]
    private void ResetLayerToDefaults()
    {
        if (SelectedLayer != null)
        {
            var index = Layers.IndexOf(SelectedLayer);
            if (index >= 0)
            {
                // Get the layer type
                var layerType = SelectedLayer.Layer.GetLayerType();
                
                // Preserve the layer name and enabled state
                var oldName = SelectedLayer.Layer.Name;
                var oldEnabled = SelectedLayer.Layer.IsEnabled;
                
                // Create a new instance with default values
                var newLayer = LayerFactory.CreateLayer(layerType);
                newLayer.Name = oldName;
                newLayer.IsEnabled = oldEnabled;
                
                // Replace in layer chain
                _layerChain.RemoveLayerAt(index);
                _layerChain.InsertLayer(index, newLayer);
                
                // Replace in Layers collection
                var newLayerViewModel = new LayerViewModel(newLayer);
                
                // Subscribe to parameter changes
                foreach (var param in newLayerViewModel.Parameters)
                {
                    param.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ParameterProperty.Value))
                        {
                            CheckAndUpdateStreaming();
                            if (!IsStreamingActive)
                            {
                                RenderChain();
                            }
                        }
                    };
                }
                
                // Update file parameter visibility when parameters change
                newLayerViewModel.Parameters.CollectionChanged += (s, e) =>
                {
                    UpdateFileParameterVisibility();
                };
                
                // Subscribe to enabled changes
                newLayerViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LayerViewModel.IsEnabled))
                    {
                        CheckAndUpdateStreaming();
                        if (!IsStreamingActive)
                        {
                            RenderChain();
                        }
                    }
                };
                
                Layers[index] = newLayerViewModel;
                SelectedLayer = newLayerViewModel;
                
                UpdateFileParameterVisibility();
                CheckAndUpdateStreaming();
                
                if (!IsStreamingActive)
                {
                    RenderChain();
                }
            }
        }
    }

    [RelayCommand]
    private void MoveLayerDown()
    {
        if (SelectedLayer != null)
        {
            var index = Layers.IndexOf(SelectedLayer);
            if (index >= 0 && index < Layers.Count - 1)
            {
                _layerChain.MoveLayer(index, index + 1);
                Layers.Move(index, index + 1);
                RenderChain();
            }
        }
    }

    /// <summary>
    /// Moves a layer from one position to another (used for drag and drop reordering)
    /// </summary>
    public void MoveLayer(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Layers.Count)
            return;
        if (toIndex < 0 || toIndex >= Layers.Count)
            return;
        if (fromIndex == toIndex)
            return;

        _layerChain.MoveLayer(fromIndex, toIndex);
        Layers.Move(fromIndex, toIndex);
        RenderChain();
    }

    [RelayCommand]
    private async Task SaveChainAsync()
    {
        try
        {
            string? filePath = null;
            
            // Check if there's a last loaded file path
            var lastFile = SessionManager.GetLastLoadedFile();
            if (lastFile != null && File.Exists(lastFile))
            {
                // Save to the last loaded file path
                filePath = lastFile;
            }
            else
            {
                // Show save dialog
                filePath = await ShowSaveDialogAsync();
            }
            
            if (filePath != null)
            {
                var xml = LayerChainSerializer.Serialize(_layerChain);
                await File.WriteAllTextAsync(filePath, xml);
                
                // Update the session with the saved file path
                SessionManager.SaveLastLoadedFile(filePath);
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving chain: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task SaveAsChainAsync()
    {
        try
        {
            var filePath = await ShowSaveDialogAsync();
            
            if (filePath != null)
            {
                var xml = LayerChainSerializer.Serialize(_layerChain);
                await File.WriteAllTextAsync(filePath, xml);
                
                // Update the session with the saved file path
                SessionManager.SaveLastLoadedFile(filePath);
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving chain: {ex.Message}");
        }
    }
    
    private async Task<string?> ShowSaveDialogAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Layer Chain",
            DefaultExtension = "xml",
            SuggestedFileName = "layerchain.xml",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("XML Files")
                {
                    Patterns = new[] { "*.xml" }
                }
            }
        });

        if (file != null && file.Path.IsFile)
        {
            return file.Path.LocalPath;
        }
        
        return null;
    }

    [RelayCommand]
    private async Task LoadChainAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Layer Chain",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("XML Files")
                {
                    Patterns = new[] { "*.xml" }
                }
            }
        });

        if (files.Count > 0 && files[0].Path.IsFile)
        {
            var filePath = files[0].Path.LocalPath;
            await LoadChainFromFileAsync(filePath);
        }
    }
    
    /// <summary>
    /// Loads a chain from a file path (used by both manual load and session restore)
    /// </summary>
    private async Task LoadChainFromFileAsync(string filePath)
    {
        try
        {
            var xml = await File.ReadAllTextAsync(filePath);
            var chain = LayerChainSerializer.Deserialize(xml);
            
            // Replace current chain
            StopStreaming();
            _layerChain.Clear();
            Layers.Clear();
            
            foreach (var layer in chain.Layers)
            {
                AddLayerToChain(layer, false);
            }
            
            if (Layers.Count > 0)
            {
                SelectedLayer = Layers[0];
            }
            
            // Save the loaded file path for session restore
            SessionManager.SaveLastLoadedFile(filePath);
            
            CheckAndUpdateStreaming();
            
            if (!IsStreamingActive)
            {
                RenderChain();
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error loading chain: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Restores the last session by loading the last saved file if it exists
    /// </summary>
    public async Task RestoreLastSessionAsync()
    {
        var lastFile = SessionManager.GetLastLoadedFile();
        if (lastFile != null)
        {
            await LoadChainFromFileAsync(lastFile);
        }
    }

    private void AddLayerToChain(BaseLayer layer, bool renderAfter = true)
    {
        _layerChain.AddLayer(layer);
        var layerViewModel = new LayerViewModel(layer);
        
        // Subscribe to parameter changes
        foreach (var param in layerViewModel.Parameters)
        {
            param.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ParameterProperty.Value))
                {
                    CheckAndUpdateStreaming();
                    if (!IsStreamingActive)
                    {
                        RenderChain();
                    }
                }
            };
        }
        
        // Update file parameter visibility when parameters change
        layerViewModel.Parameters.CollectionChanged += (s, e) =>
        {
            UpdateFileParameterVisibility();
        };
        
        // Subscribe to enabled changes
        layerViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LayerViewModel.IsEnabled))
            {
                CheckAndUpdateStreaming();
                if (!IsStreamingActive)
                {
                    RenderChain();
                }
            }
        };
        
        Layers.Add(layerViewModel);
        
        if (SelectedLayer == null)
        {
            SelectedLayer = layerViewModel;
        }
        
        CheckAndUpdateStreaming();
        
        if (renderAfter && !IsStreamingActive)
        {
            RenderChain();
        }
    }

    private void RenderChain()
    {
        // For streaming, allow concurrent renders to prevent blocking
        // The old frame will remain visible until the new one is ready
        if (IsStreamingActive)
        {
            // During streaming, allow multiple renders in parallel
            // Each render will update DisplayImage when ready, keeping old frame visible
            Task.Run(() =>
            {
                RenderChainInternal();
            });
        }
        else
        {
            // For non-streaming, only render if not already rendering
            if (IsRendering)
                return;

            IsRendering = true;
            Task.Run(() =>
            {
                RenderChainInternal();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsRendering = false;
                });
            });
        }
    }

    private void RenderChainInternal()
    {
        try
        {
            // Capture RenderMode at the start to ensure consistency throughout the render
            // This prevents race conditions when switching modes during streaming
            var currentRenderMode = RenderMode;
            Mat? result = null;
            var errors = new List<string>();

            if (SelectedLayer != null)
            {
                var selectedIndex = Layers.IndexOf(SelectedLayer);
                if (selectedIndex >= 0)
                {
                    result = _layerChain.RenderUpTo(selectedIndex, _sourceImage, currentRenderMode);
                }
            }
            else if (_sourceImage != null)
            {
                result = _sourceImage.Clone();
            }
            else if (_layerChain.Count > 0)
            {
                // Render full chain if no selection
                result = _layerChain.RenderChain(_sourceImage, currentRenderMode);
            }

            // Collect errors from layers
            foreach (var layer in Layers)
            {
                if (layer.Layer.LastError != null)
                {
                    errors.Add(layer.Layer.LastError);
                }
            }

            var bitmap = ImageConverter.MatToBitmapSafe(result);
            
            // Update UI on main thread
            // Old frame stays visible until this update happens
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DisplayImage = bitmap;
                
                // Refresh runtime display
                foreach (var layer in Layers)
                {
                    layer.RefreshRuntime();
                }
                
                // Update total runtime
                OnPropertyChanged(nameof(TotalRuntime));

                // Update error log
                ErrorLog.Clear();
                foreach (var error in errors)
                {
                    ErrorLog.Add(error);
                }
                HasErrors = ErrorLog.Count > 0;
            });

            result?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering: {ex.Message}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ErrorLog.Clear();
                ErrorLog.Add($"Rendering error: {ex.Message}");
                HasErrors = true;
            });
        }
    }


    private void CheckAndUpdateStreaming()
    {
        // Check if there's a streaming layer (WebcamLayer or VideoLayer) with streaming enabled
        var webcamLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.WebcamLayer>()
            .FirstOrDefault(l => l.IsEnabled && l.Streaming);
        var videoLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.VideoLayer>()
            .FirstOrDefault(l => l.IsEnabled && l.Streaming);

        var hasStreamingLayer = webcamLayer != null || videoLayer != null;

        if (hasStreamingLayer && !IsStreamingActiveInternal())
        {
            StartStreaming(webcamLayer, videoLayer);
            OnPropertyChanged(nameof(IsStreamingActive));
            OnPropertyChanged(nameof(CanStepFrames));
            StepFrameForwardCommand.NotifyCanExecuteChanged();
            StepFrameBackwardCommand.NotifyCanExecuteChanged();
        }
        else if (!hasStreamingLayer && IsStreamingActiveInternal())
        {
            StopStreaming();
            OnPropertyChanged(nameof(IsStreamingActive));
        }
        else if (hasStreamingLayer && IsStreamingActiveInternal())
        {
            // Update streaming parameters if needed (FPS change, etc.)
            // The streaming loop will pick up the new FPS on next iteration
            OnPropertyChanged(nameof(CanStepFrames));
            StepFrameForwardCommand.NotifyCanExecuteChanged();
            StepFrameBackwardCommand.NotifyCanExecuteChanged();
        }
    }

    private void StartStreaming(OpenCVGui.Lib.Import.WebcamLayer? webcamLayer, OpenCVGui.Lib.Import.VideoLayer? videoLayer)
    {
        StopStreaming(); // Make sure any existing stream is stopped

        _streamingCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _streamingCancellationTokenSource.Token;

        _streamingTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if paused
                    bool isPaused;
                    lock (_streamingLock)
                    {
                        isPaused = IsStreamingPaused;
                    }
                    
                    if (isPaused)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    // Get current streaming layers
                    var currentWebcamLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.WebcamLayer>()
                        .FirstOrDefault(l => l.IsEnabled && l.Streaming);
                    var currentVideoLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.VideoLayer>()
                        .FirstOrDefault(l => l.IsEnabled && l.Streaming);
                    
                    if (currentWebcamLayer == null && currentVideoLayer == null)
                    {
                        // Streaming was disabled, exit loop
                        break;
                    }

                    // Determine FPS from active layer
                    int targetFps = 0;
                    double videoFps = 0;
                    if (currentWebcamLayer != null)
                    {
                        targetFps = currentWebcamLayer.TargetFps;
                    }
                    else if (currentVideoLayer != null)
                    {
                        targetFps = currentVideoLayer.TargetFps;
                        videoFps = currentVideoLayer.VideoFps;
                    }

                    int delayMs = 0;
                    if (targetFps > 0)
                    {
                        delayMs = 1000 / targetFps;
                    }
                    else if (currentVideoLayer != null && videoFps > 0)
                    {
                        // For video with TargetFps = 0, use video's native FPS
                        delayMs = (int)(1000.0 / videoFps);
                    }

                    RenderChain();

                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    else
                    {
                        // Maximum speed - small delay to prevent CPU spinning
                        await Task.Delay(1, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in streaming loop: {ex.Message}");
                    await Task.Delay(100, cancellationToken); // Wait before retrying
                }
            }
        }, cancellationToken);
        
        // Notify UI that streaming has started
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(IsStreamingActive));
        });
    }

    private void StopStreaming()
    {
        if (_streamingCancellationTokenSource != null)
        {
            _streamingCancellationTokenSource.Cancel();
            _streamingCancellationTokenSource.Dispose();
            _streamingCancellationTokenSource = null;
        }

        if (_streamingTask != null)
        {
            try
            {
                _streamingTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore timeout
            }
            _streamingTask = null;
        }
        
        lock (_streamingLock)
        {
            IsStreamingPaused = false;
        }
        
        OnPropertyChanged(nameof(IsStreamingActive));
        OnPropertyChanged(nameof(IsStreamingPaused));
        OnPropertyChanged(nameof(IsStreamingPlaying));
        OnPropertyChanged(nameof(PauseButtonIcon));
        OnPropertyChanged(nameof(PauseButtonTooltip));
        OnPropertyChanged(nameof(CanStepFrames));
        StepFrameForwardCommand.NotifyCanExecuteChanged();
        StepFrameBackwardCommand.NotifyCanExecuteChanged();
    }

    private static TopLevel? GetTopLevel()
    {
        // Try to get the current top level from the application
        return Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}

public class LayerTypeItem
{
    public string LayerTypeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    
    public override string ToString() => DisplayName;
}
