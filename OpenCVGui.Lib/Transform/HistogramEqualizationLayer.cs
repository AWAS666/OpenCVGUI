using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that performs global histogram equalization
/// </summary>
public class HistogramEqualizationLayer : BaseLayer
{
    [Parameter(DisplayName = "Apply to Color", Description = "If true, applies equalization to each color channel separately (for color images)", Order = 0)]
    public bool ApplyToColor { get; set; } = false;

    public override string GetLayerType() => "HistogramEqualization";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat result = new Mat();

        if (input.Channels() == 1)
        {
            // Grayscale: apply histogram equalization directly
            Cv2.EqualizeHist(input, result);
        }
        else if (input.Channels() == 3)
        {
            if (ApplyToColor)
            {
                // Apply to each channel separately
                Mat[] channels = Cv2.Split(input);
                Mat[] equalizedChannels = new Mat[3];

                for (int i = 0; i < 3; i++)
                {
                    equalizedChannels[i] = new Mat();
                    Cv2.EqualizeHist(channels[i], equalizedChannels[i]);
                    channels[i].Dispose();
                }

                Cv2.Merge(equalizedChannels, result);

                foreach (var channel in equalizedChannels)
                {
                    channel.Dispose();
                }
            }
            else
            {
                // Convert to YUV, equalize Y channel, convert back
                Mat yuv = new Mat();
                Cv2.CvtColor(input, yuv, ColorConversionCodes.BGR2YUV);

                Mat[] channels = Cv2.Split(yuv);
                Cv2.EqualizeHist(channels[0], channels[0]);

                Cv2.Merge(channels, yuv);
                Cv2.CvtColor(yuv, result, ColorConversionCodes.YUV2BGR);

                foreach (var channel in channels)
                {
                    channel.Dispose();
                }
                yuv.Dispose();
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

