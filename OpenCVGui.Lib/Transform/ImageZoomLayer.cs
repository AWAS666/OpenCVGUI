using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that zooms an image by a specified factor
/// </summary>
public class ImageZoomLayer : BaseLayer
{
    [Parameter(DisplayName = "Zoom Factor", Description = "Zoom factor (1.0 = no zoom, 2.0 = double size, 0.5 = half size)", Order = 0)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double ZoomFactor { get; set; } = 1.0;

    [Parameter(DisplayName = "Interpolation", Description = "Interpolation method for resizing", Order = 1)]
    public InterpolationFlags Interpolation { get; set; } = InterpolationFlags.Linear;

    public override string GetLayerType() => "ImageZoom";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        var newWidth = (int)(input.Width * ZoomFactor);
        var newHeight = (int)(input.Height * ZoomFactor);

        var result = new Mat();
        Cv2.Resize(input, result, new Size(newWidth, newHeight), 0, 0, Interpolation);
        return result;
    }
}

