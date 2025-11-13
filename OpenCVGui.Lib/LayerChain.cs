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

        if (renderMode == RenderMode.UI)
        {
            // In UI mode: render previous layers in Chain mode to get processing output,
            // then render current layer in UI mode with both chain output (for processing)
            // and UI output from previous layers (for visualization)
            Mat? chainOutput = inputImage;
            Mat? uiOutput = inputImage;
            Mat? result = null;
            List<Mat> intermediateMats = new List<Mat>();

            try
            {
                // Render all previous layers in Chain mode to get processing output
                for (int i = 0; i < layerIndex; i++)
                {
                    var layer = _layers[i];
                    if (layer.IsEnabled)
                    {
                        try
                        {
                            var newChainOutput = layer.Render(chainOutput, RenderMode.Chain);
                            // Track intermediate results for disposal (but not inputImage)
                            if (chainOutput != inputImage && chainOutput != null)
                            {
                                intermediateMats.Add(chainOutput);
                            }
                            chainOutput = newChainOutput;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in layer {layer.GetLayerType()}: {ex.Message}");
                            if (chainOutput == null || chainOutput.Empty())
                            {
                                chainOutput = new Mat();
                            }
                        }
                    }
                }

                // Render previous layers in UI mode to get visualization output
                for (int i = 0; i < layerIndex; i++)
                {
                    var layer = _layers[i];
                    if (layer.IsEnabled)
                    {
                        try
                        {
                            var newUiOutput = layer.Render(uiOutput, RenderMode.UI);
                            // Track intermediate results for disposal (but not inputImage)
                            if (uiOutput != inputImage && uiOutput != null)
                            {
                                intermediateMats.Add(uiOutput);
                            }
                            uiOutput = newUiOutput;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in layer {layer.GetLayerType()}: {ex.Message}");
                            if (uiOutput == null || uiOutput.Empty())
                            {
                                uiOutput = new Mat();
                            }
                        }
                    }
                }

                // Render current layer in UI mode, passing both UI output (for drawing)
                // and chain output (for processing)
                var currentLayer = _layers[layerIndex];
                if (currentLayer.IsEnabled)
                {
                    try
                    {
                        // Pass uiOutput as the main input (for visualization/drawing)
                        // and chainOutput as chainInput (for processing)
                        result = currentLayer.Render(uiOutput, RenderMode.UI, chainOutput);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in layer {currentLayer.GetLayerType()}: {ex.Message}");
                        result = uiOutput?.Clone() ?? new Mat();
                    }
                }
                else
                {
                    result = uiOutput?.Clone() ?? new Mat();
                }

                // Cleanup intermediate results (they've been cloned or used)
                // Don't dispose chainOutput or uiOutput if they're the same as result
                if (chainOutput != inputImage && chainOutput != null && chainOutput != result)
                {
                    intermediateMats.Add(chainOutput);
                }
                if (uiOutput != inputImage && uiOutput != null && uiOutput != result)
                {
                    intermediateMats.Add(uiOutput);
                }

                // Dispose all intermediate Mat objects
                foreach (var mat in intermediateMats)
                {
                    if (mat != null && !mat.IsDisposed)
                    {
                        mat.Dispose();
                    }
                }

                return result ?? new Mat();
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
                if (chainOutput != inputImage && chainOutput != null && !chainOutput.IsDisposed)
                {
                    chainOutput.Dispose();
                }
                if (uiOutput != inputImage && uiOutput != null && !uiOutput.IsDisposed)
                {
                    uiOutput.Dispose();
                }
                if (result != null && !result.IsDisposed)
                {
                    result.Dispose();
                }
                throw;
            }
        }
        else
        {
            // In Chain mode: all layers render in Chain mode
            Mat? current = inputImage;

            for (int i = 0; i <= layerIndex; i++)
            {
                var layer = _layers[i];
                if (layer.IsEnabled)
                {
                    try
                    {
                        current = layer.Render(current, RenderMode.Chain);
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

            return current ?? new Mat();
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

