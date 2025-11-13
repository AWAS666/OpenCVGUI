using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Flip direction options
/// </summary>
public enum FlipDirection
{
    /// <summary>
    /// Flip horizontally (around vertical axis)
    /// </summary>
    Horizontal,

    /// <summary>
    /// Flip vertically (around horizontal axis)
    /// </summary>
    Vertical,

    /// <summary>
    /// Flip both horizontally and vertically
    /// </summary>
    Both
}

/// <summary>
/// Layer that flips an image horizontally, vertically, or both
/// </summary>
public class ImageFlipLayer : BaseLayer
{
    [Parameter(DisplayName = "Flip Direction", Description = "Direction to flip the image", Order = 0)]
    public FlipDirection Direction { get; set; } = FlipDirection.Horizontal;

    public override string GetLayerType() => "ImageFlip";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Convert direction to OpenCV flip mode
        FlipMode flipMode = Direction switch
        {
            FlipDirection.Horizontal => FlipMode.Y,  // Flip around vertical axis
            FlipDirection.Vertical => FlipMode.X,     // Flip around horizontal axis
            FlipDirection.Both => FlipMode.XY,        // Flip both
            _ => FlipMode.Y
        };

        Mat result = new Mat();
        Cv2.Flip(input, result, flipMode);

        return result;
    }
}

