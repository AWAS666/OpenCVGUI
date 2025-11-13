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
    public long LastRuntime { get; protected set; }

    /// <summary>
    /// Returns the unique type identifier for this layer class
    /// </summary>
    public abstract string GetLayerType();

    /// <summary>
    /// Can expect input and returns output
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected abstract Mat RenderInternal(Mat? input);

    public Mat Render(Mat? input)
    {
        if (!IsEnabled)
        {
            return input ?? new Mat();
        }

        var watch = Stopwatch.StartNew();
        var result = RenderInternal(input);
        LastRuntime = watch.ElapsedMilliseconds;
        return result;
    }
}
