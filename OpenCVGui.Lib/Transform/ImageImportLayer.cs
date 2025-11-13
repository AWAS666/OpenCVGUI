using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that imports an image from a file path. This is typically the first layer in a chain.
/// </summary>
public class ImageImportLayer : BaseLayer
{
    [Parameter(DisplayName = "Image Path", Description = "Path to the image file to load", Order = 0)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    public string ImagePath { get; set; } = string.Empty;

    public override string GetLayerType() => "ImageImport";

    protected override Mat RenderInternal(Mat? input)
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            return new Mat();
        }

        var image = Cv2.ImRead(ImagePath, ImreadModes.Color);
        if (image.Empty())
        {
            return new Mat();
        }

        return image;
    }
}

