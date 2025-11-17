using OpenCvSharp;
using System.Collections.ObjectModel;

namespace OpenCVGui.Lib;

/// <summary>
/// Manages an ordered collection of layers that process images in sequence
/// </summary>
public class LayerChain
{
    private readonly List<BaseLayer> _layers = new();

    /// <summary>
    /// Read-only collection of layers in the chain
    /// </summary>
    public ReadOnlyCollection<BaseLayer> Layers => _layers.AsReadOnly();

    /// <summary>
    /// Number of layers in the chain
    /// </summary>
    public int Count => _layers.Count;

    /// <summary>
    /// Adds a layer to the end of the chain
    /// </summary>
    public void AddLayer(BaseLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));

        _layers.Add(layer);
    }

    /// <summary>
    /// Inserts a layer at the specified index
    /// </summary>
    public void InsertLayer(int index, BaseLayer layer)
    {
        if (layer == null)
            throw new ArgumentNullException(nameof(layer));
        if (index < 0 || index > _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _layers.Insert(index, layer);
    }

    /// <summary>
    /// Removes a layer from the chain
    /// </summary>
    public bool RemoveLayer(BaseLayer layer)
    {
        return _layers.Remove(layer);
    }

    /// <summary>
    /// Removes a layer at the specified index
    /// </summary>
    public void RemoveLayerAt(int index)
    {
        if (index < 0 || index >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _layers.RemoveAt(index);
    }

    /// <summary>
    /// Moves a layer from one position to another
    /// </summary>
    public void MoveLayer(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        var layer = _layers[fromIndex];
        _layers.RemoveAt(fromIndex);
        _layers.Insert(toIndex, layer);
    }

    /// <summary>
    /// Renders the entire chain, processing the input through all enabled layers
    /// </summary>
    /// <param name="inputImage">Input image (can be null for layers that generate their own input)</param>
    /// <param name="renderMode">Rendering mode (UI or Chain)</param>
    /// <returns>Final processed image</returns>
    public Mat RenderChain(Mat? inputImage = null, RenderMode renderMode = RenderMode.Chain)
    {
        Mat? current = inputImage;

        foreach (var layer in _layers)
        {
            if (layer.IsEnabled)
            {
                try
                {
                    current = layer.Render(current, renderMode);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in layer {layer.GetLayerType()}: {ex.Message}");
                    // Continue with current output or empty mat
                    if (current == null || current.Empty())
                    {
                        current = new Mat();
                    }
                }
            }
        }

        return current ?? new Mat();
    }

    /// <summary>
    /// Renders the chain up to and including the layer at the specified index
    /// </summary>
    /// <param name="layerIndex">Index of the last layer to render (inclusive)</param>
    /// <param name="inputImage">Input image (can be null)</param>
    /// <param name="renderMode">Rendering mode (UI or Chain)</param>
    /// <returns>Image after processing through layers up to the specified index</returns>
    public Mat RenderUpTo(int layerIndex, Mat? inputImage = null, RenderMode renderMode = RenderMode.Chain)
    {
        if (layerIndex < 0 || layerIndex >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(layerIndex));

        Mat? current = inputImage;
        Mat? originalInput = inputImage; // For drawing in UI mode - will be set from first layer if inputImage is null
        List<Mat> intermediateMats = new List<Mat>();

        try
        {
            // If inputImage is null, we need to get the first layer's output as the original input
            if (inputImage == null && layerIndex >= 0)
            {
                var firstLayer = _layers[0];
                if (firstLayer.IsEnabled)
                {
                    try
                    {
                        originalInput = firstLayer.Render(null, RenderMode.Chain);
                        current = originalInput.Clone();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in first layer {firstLayer.GetLayerType()}: {ex.Message}");
                        originalInput = new Mat();
                        current = new Mat();
                    }
                }
            }

            // Render all layers up to and including the current one
            // Skip the first layer if inputImage was null (we already rendered it above)
            int startIndex = (inputImage == null) ? 1 : 0;
            for (int i = startIndex; i <= layerIndex; i++)
            {
                var layer = _layers[i];
                if (layer.IsEnabled)
                {
                    try
                    {
                        // In UI mode, pass originalInput as chainInput for drawing
                        // In Chain mode, chainInput is null (not used)
                        Mat? chainInput = (renderMode == RenderMode.UI) ? originalInput : null;
                        var newCurrent = layer.Render(current, renderMode, chainInput);
                        
                        // Track intermediate results for disposal (but not originalInput or inputImage)
                        if (current != inputImage && current != originalInput && current != null)
                        {
                            intermediateMats.Add(current);
                        }
                        current = newCurrent;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in layer {layer.GetLayerType()}: {ex.Message}");
                        if (current == null || current.Empty())
                        {
                            current = new Mat();
                        }
                    }
                }
            }

            // Cleanup intermediate results (they've been cloned or used)
            if (originalInput != inputImage && originalInput != null && originalInput != current)
            {
                intermediateMats.Add(originalInput);
            }

            // Dispose all intermediate Mat objects
            foreach (var mat in intermediateMats)
            {
                if (mat != null && !mat.IsDisposed)
                {
                    mat.Dispose();
                }
            }

            return current ?? new Mat();
        }
        catch
        {
            // Cleanup on error
            foreach (var mat in intermediateMats)
            {
                if (mat != null && !mat.IsDisposed)
                {
                    mat.Dispose();
                }
            }
            if (current != inputImage && current != originalInput && current != null && !current.IsDisposed)
            {
                current.Dispose();
            }
            if (originalInput != inputImage && originalInput != null && !originalInput.IsDisposed)
            {
                originalInput.Dispose();
            }
            throw;
        }
    }

    /// <summary>
    /// Clears all layers from the chain
    /// </summary>
    public void Clear()
    {
        _layers.Clear();
    }

    /// <summary>
    /// Gets the layer at the specified index
    /// </summary>
    public BaseLayer this[int index] => _layers[index];
}

