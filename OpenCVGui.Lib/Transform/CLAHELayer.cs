using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that performs Contrast Limited Adaptive Histogram Equalization (CLAHE)
/// </summary>
public class CLAHELayer : BaseLayer
{
    [Parameter(DisplayName = "Clip Limit", Description = "Threshold for contrast limiting (higher = more contrast)", Order = 0)]
    [ParameterRange(1.0, 10.0, 0.1)]
    public double ClipLimit { get; set; } = 2.0;

    [Parameter(DisplayName = "Tile Grid Size X", Description = "Number of tiles in X direction", Order = 1)]
    [ParameterRange(2, 16, 1)]
    public int TileGridSizeX { get; set; } = 8;

    [Parameter(DisplayName = "Tile Grid Size Y", Description = "Number of tiles in Y direction", Order = 2)]
    [ParameterRange(2, 16, 1)]
    public int TileGridSizeY { get; set; } = 8;

    [Parameter(DisplayName = "Apply to Color", Description = "If true, applies CLAHE to each color channel separately (for color images)", Order = 3)]
    public bool ApplyToColor { get; set; } = false;

    public override string GetLayerType() => "CLAHE";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat result = new Mat();

        if (input.Channels() == 1)
        {
            // Grayscale: apply CLAHE directly
            using (var clahe = Cv2.CreateCLAHE(ClipLimit, new Size(TileGridSizeX, TileGridSizeY)))
            {
                clahe.Apply(input, result);
            }
        }
        else if (input.Channels() == 3)
        {
            if (ApplyToColor)
            {
                // Apply to each channel separately
                Mat[] channels = Cv2.Split(input);
                Mat[] equalizedChannels = new Mat[3];

                using (var clahe = Cv2.CreateCLAHE(ClipLimit, new Size(TileGridSizeX, TileGridSizeY)))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        equalizedChannels[i] = new Mat();
                        clahe.Apply(channels[i], equalizedChannels[i]);
                        channels[i].Dispose();
                    }
                }

                Cv2.Merge(equalizedChannels, result);

                foreach (var channel in equalizedChannels)
                {
                    channel.Dispose();
                }
            }
            else
            {
                // Convert to LAB, apply CLAHE to L channel, convert back
                Mat lab = new Mat();
                Cv2.CvtColor(input, lab, ColorConversionCodes.BGR2Lab);

                Mat[] channels = Cv2.Split(lab);
                using (var clahe = Cv2.CreateCLAHE(ClipLimit, new Size(TileGridSizeX, TileGridSizeY)))
                {
                    clahe.Apply(channels[0], channels[0]);
                }

                Cv2.Merge(channels, lab);
                Cv2.CvtColor(lab, result, ColorConversionCodes.Lab2BGR);

                foreach (var channel in channels)
                {
                    channel.Dispose();
                }
                lab.Dispose();
            }
        }
        else
        {
            // For other channel counts, just clone
            result = input.Clone();
        }

        return result;
    }
}

