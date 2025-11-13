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
    private readonly RenderCache _renderCache = new();
    private Mat? _sourceImage;
    private CancellationTokenSource? _streamingCancellationTokenSource;
    private Task? _streamingTask;

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

    public bool IsUIRenderMode => RenderMode == RenderMode.UI;
    
    public bool IsChainRenderMode => RenderMode == RenderMode.Chain;

    public IBrush UIRenderModeIconColor => IsUIRenderMode ? Brushes.Green : Brushes.Gray;
    
    public IBrush ChainRenderModeIconColor => IsChainRenderMode ? Brushes.Blue : Brushes.Gray;

    partial void OnRenderModeChanged(RenderMode value)
    {
        OnPropertyChanged(nameof(IsUIRenderMode));
        OnPropertyChanged(nameof(IsChainRenderMode));
        OnPropertyChanged(nameof(UIRenderModeIconColor));
        OnPropertyChanged(nameof(ChainRenderModeIconColor));
        RenderChain();
    }

    [RelayCommand]
    private void SetRenderMode(string mode)
    {
        if (Enum.TryParse<RenderMode>(mode, out var renderMode))
        {
            RenderMode = renderMode;
        }
    }

    partial void OnSelectedLayerChanged(LayerViewModel? value)
    {
        UpdateFileParameterVisibility();
        RenderChain();
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
            SelectedLayerType = null; // Reset selection
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

    [RelayCommand]
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
                
                if (!IsStreamingActive())
                {
                    RenderChain();
                }
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

    [RelayCommand]
    private async Task SaveChainAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
            return;

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
            try
            {
                var xml = LayerChainSerializer.Serialize(_layerChain);
                await File.WriteAllTextAsync(file.Path.LocalPath, xml);
            }
            catch (Exception ex)
            {
                // TODO: Show error dialog
                System.Diagnostics.Debug.WriteLine($"Error saving chain: {ex.Message}");
            }
        }
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
            try
            {
                var xml = await File.ReadAllTextAsync(files[0].Path.LocalPath);
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
                
                CheckAndUpdateStreaming();
                
                if (!IsStreamingActive())
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
                    if (!IsStreamingActive())
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
                if (!IsStreamingActive())
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
        
        if (renderAfter && !IsStreamingActive())
        {
            RenderChain();
        }
    }

    private void RenderChain()
    {
        // For streaming, allow concurrent renders to prevent blocking
        // The old frame will remain visible until the new one is ready
        if (IsStreamingActive())
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
            Mat? result = null;

                if (SelectedLayer != null)
                {
                    var selectedIndex = Layers.IndexOf(SelectedLayer);
                    if (selectedIndex >= 0)
                    {
                        result = _layerChain.RenderUpTo(selectedIndex, _sourceImage, RenderMode);
                    }
                }
                else if (_sourceImage != null)
                {
                    result = _sourceImage.Clone();
                }
                else if (_layerChain.Count > 0)
                {
                    // Render full chain if no selection
                    result = _layerChain.RenderChain(_sourceImage, RenderMode);
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
            });

            result?.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering: {ex.Message}");
        }
    }

    private bool IsStreamingActive()
    {
        return _streamingTask != null && !_streamingTask.IsCompleted;
    }

    private void CheckAndUpdateStreaming()
    {
        // Check if there's a WebcamLayer with streaming enabled
        var webcamLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.WebcamLayer>()
            .FirstOrDefault(l => l.IsEnabled && l.Streaming);

        if (webcamLayer != null && !IsStreamingActive())
        {
            StartStreaming(webcamLayer);
        }
        else if (webcamLayer == null && IsStreamingActive())
        {
            StopStreaming();
        }
        else if (webcamLayer != null && IsStreamingActive())
        {
            // Update streaming parameters if needed (FPS change, etc.)
            // The streaming loop will pick up the new FPS on next iteration
        }
    }

    private void StartStreaming(OpenCVGui.Lib.Import.WebcamLayer webcamLayer)
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
                    // Get current webcam layer and FPS setting (in case it changed)
                    var currentWebcamLayer = _layerChain.Layers.OfType<OpenCVGui.Lib.Import.WebcamLayer>()
                        .FirstOrDefault(l => l.IsEnabled && l.Streaming);
                    
                    if (currentWebcamLayer == null)
                    {
                        // Streaming was disabled, exit loop
                        break;
                    }

                    int targetFps = currentWebcamLayer.TargetFps;
                    int delayMs = targetFps > 0 ? 1000 / targetFps : 0;

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
