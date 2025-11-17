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

    /// <summary>
    /// Gets the appropriate draw target based on render mode.
    /// In UI mode: returns chainInput (original user input) if available, otherwise input.
    /// In Chain mode: returns input (chain output).
    /// </summary>
    protected Mat? GetDrawTarget(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (renderMode == RenderMode.UI && chainInput != null && !chainInput.Empty())
        {
            return chainInput;
        }
        return input;
    }

    /// <summary>
    /// Prepares a BGR Mat for drawing from the draw target.
    /// Clones the target if it's already BGR, or converts from grayscale to BGR.
    /// </summary>
    protected Mat PrepareDrawTarget(Mat? drawTarget)
    {
        if (drawTarget == null || drawTarget.Empty())
        {
            return new Mat();
        }

        if (drawTarget.Channels() == 3)
        {
            return drawTarget.Clone();
        }
        else
        {
            Mat result = new Mat();
            Cv2.CvtColor(drawTarget, result, ColorConversionCodes.GRAY2BGR);
            return result;
        }
    }

    public Mat Render(Mat? input, RenderMode renderMode = RenderMode.Chain, Mat? chainInput = null)
    {
        if (!IsEnabled)
        {
            return input ?? new Mat();
        }

        var watch = Stopwatch.StartNew();
        const int timeoutSeconds = 5;
        
        try
        {
            Mat? result = null;
            Exception? renderException = null;
            var renderCompleted = new System.Threading.ManualResetEventSlim(false);
            
            // Run render in a separate task with timeout
            var renderTask = Task.Run(() =>
            {
                try
                {
                    result = RenderInternal(input, renderMode, chainInput);
                }
                catch (Exception ex)
                {
                    renderException = ex;
                }
                finally
                {
                    renderCompleted.Set();
                }
            });

            // Wait for completion or timeout
            if (renderCompleted.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                // Render completed
                if (renderException != null)
                {
                    throw renderException;
                }
                
                LastRuntime = watch.Elapsed.TotalMilliseconds;
                LastError = null;
                return result ?? (input?.Clone() ?? new Mat());
            }
            else
            {
                // Timeout occurred
                LastRuntime = watch.Elapsed.TotalMilliseconds;
                var layerName = string.IsNullOrEmpty(Name) ? GetLayerType() : $"{GetLayerType()} ({Name})";
                LastError = $"{layerName}: Processing timeout after {timeoutSeconds} seconds";
                System.Diagnostics.Debug.WriteLine($"Layer timeout: {LastError}");
                
                // Return input or empty mat to continue chain
                return input?.Clone() ?? new Mat();
            }
        }
        catch (Exception ex)
        {
            LastRuntime = watch.Elapsed.TotalMilliseconds;
            LastError = $"{GetLayerType()}{(string.IsNullOrEmpty(Name) ? "" : $" ({Name})")}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Layer error: {LastError}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Return input or empty mat to continue chain
            return input?.Clone() ?? new Mat();
        }
    }

    /// <summary>
    /// Last error message from rendering (null if no error)
    /// </summary>
    public string? LastError { get; private set; }
}
