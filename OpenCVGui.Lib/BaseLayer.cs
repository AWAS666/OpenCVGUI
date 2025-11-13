using OpenCvSharp;
using System.Diagnostics;

namespace OpenCVGui.Lib;

public abstract class BaseLayer
{
    /// <summary>
    /// Unique identifier for this layer instance
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for this layer instance
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this layer is enabled in the chain
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Runtime of last render call in milliseconds
    /// </summary>
    public double LastRuntime { get; protected set; }

    /// <summary>
    /// Returns the unique type identifier for this layer class
    /// </summary>
    public abstract string GetLayerType();

    /// <summary>
    /// Can expect input and returns output
    /// </summary>
    /// <param name="input">UI input (for visualization)</param>
    /// <param name="renderMode">Rendering mode (UI or Chain)</param>
    /// <param name="chainInput">Optional chain input (for processing when in UI mode)</param>
    /// <returns></returns>
    protected abstract Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null);

    public Mat Render(Mat? input, RenderMode renderMode = RenderMode.Chain, Mat? chainInput = null)
    {
        if (!IsEnabled)
        {
            return input ?? new Mat();
        }

        var watch = Stopwatch.StartNew();
        var result = RenderInternal(input, renderMode, chainInput);
        LastRuntime = watch.Elapsed.TotalMilliseconds;
        return result;
    }
}
