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
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;

namespace OpenCVGui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly LayerChain _layerChain = new();
    private readonly RenderCache _renderCache = new();
    private Mat? _sourceImage;

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
                
                RenderChain();
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
                
                RenderChain();
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
                    RenderChain();
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
                RenderChain();
            }
        };
        
        Layers.Add(layerViewModel);
        
        if (SelectedLayer == null)
        {
            SelectedLayer = layerViewModel;
        }
        
        if (renderAfter)
        {
            RenderChain();
        }
    }

    private void RenderChain()
    {
        if (IsRendering)
            return;

        IsRendering = true;

        Task.Run(() =>
        {
            try
            {
                Mat? result = null;

                if (SelectedLayer != null)
                {
                    var selectedIndex = Layers.IndexOf(SelectedLayer);
                    if (selectedIndex >= 0)
                    {
                        result = _layerChain.RenderUpTo(selectedIndex, _sourceImage);
                    }
                }
                else if (_sourceImage != null)
                {
                    result = _sourceImage.Clone();
                }
                else if (_layerChain.Count > 0)
                {
                    // Render full chain if no selection
                    result = _layerChain.RenderChain(_sourceImage);
                }

                var bitmap = ImageConverter.MatToBitmapSafe(result);
                
                // Update UI on main thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    DisplayImage = bitmap;
                    IsRendering = false;
                    
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
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsRendering = false;
                });
            }
        });
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
