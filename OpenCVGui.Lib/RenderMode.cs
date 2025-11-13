namespace OpenCVGui.Lib;

/// <summary>
/// Rendering mode that determines how layers should render their output
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// UI mode - optimized for visualization and user feedback
    /// </summary>
    UI,

    /// <summary>
    /// Chain mode - optimized for processing pipeline (actual output for next layer)
    /// </summary>
    Chain
}

