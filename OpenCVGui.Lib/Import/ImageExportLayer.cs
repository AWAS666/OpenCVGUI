using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Import;

/// <summary>
/// Layer that exports the processed image to a file. This is typically the last layer in a chain.
/// </summary>
public class ImageExportLayer : BaseLayer
{
    [Parameter(DisplayName = "Export Path", Description = "Path where the image will be saved", Order = 0)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif")]
    public string ExportPath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Auto Export", Description = "If true, automatically exports when the image is processed", Order = 1)]
    public bool AutoExport { get; set; } = false;

    [Parameter(DisplayName = "JPEG Quality", Description = "JPEG quality (1-100, only used for JPEG files)", Order = 2)]
    [ParameterRange(1, 100, 1)]
    public int JpegQuality { get; set; } = 95;

    public override string GetLayerType() => "ImageExport";

    protected override Mat RenderInternal(Mat? input)
    {
        if (input == null || input.Empty())
        {
            return input ?? new Mat();
        }

        // If auto export is enabled and path is set, save the image
        if (AutoExport && !string.IsNullOrEmpty(ExportPath))
        {
            try
            {
                var extension = Path.GetExtension(ExportPath).ToLowerInvariant();
                
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    Cv2.ImWrite(ExportPath, input, new int[] { (int)ImwriteFlags.JpegQuality, JpegQuality });
                }
                else
                {
                    // PNG, BMP, and other formats
                    Cv2.ImWrite(ExportPath, input);
                }
            }
            catch
            {
                // Silently fail - export errors shouldn't break the chain
            }
        }

        // Export layer passes through the image unchanged
        return input.Clone();
    }
}

