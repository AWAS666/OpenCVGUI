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
    /// <returns>Final processed image</returns>
    public Mat RenderChain(Mat? inputImage = null)
    {
        Mat? current = inputImage;

        foreach (var layer in _layers)
        {
            if (layer.IsEnabled)
            {
                current = layer.Render(current);
            }
        }

        return current ?? new Mat();
    }

    /// <summary>
    /// Renders the chain up to and including the layer at the specified index
    /// </summary>
    /// <param name="layerIndex">Index of the last layer to render (inclusive)</param>
    /// <param name="inputImage">Input image (can be null)</param>
    /// <returns>Image after processing through layers up to the specified index</returns>
    public Mat RenderUpTo(int layerIndex, Mat? inputImage = null)
    {
        if (layerIndex < 0 || layerIndex >= _layers.Count)
            throw new ArgumentOutOfRangeException(nameof(layerIndex));

        Mat? current = inputImage;

        for (int i = 0; i <= layerIndex; i++)
        {
            var layer = _layers[i];
            if (layer.IsEnabled)
            {
                current = layer.Render(current);
            }
        }

        return current ?? new Mat();
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

