using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that extracts a single channel from a multi-channel image
/// </summary>
public class ExtractChannelLayer : BaseLayer
{
    [Parameter(DisplayName = "Channel Index", Description = "Channel index to extract (0=Blue/First, 1=Green/Second, 2=Red/Third)", Order = 0)]
    [ParameterRange(0, 2, 1)]
    public int ChannelIndex { get; set; } = 0;

    public override string GetLayerType() => "ExtractChannel";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat result = new Mat();

        if (input.Channels() == 3)
        {
            Cv2.ExtractChannel(input, result, ChannelIndex);
        }
        else if (input.Channels() == 1)
        {
            result = input.Clone();
        }
        else
        {
            if (ChannelIndex < input.Channels())
            {
                Cv2.ExtractChannel(input, result, ChannelIndex);
            }
            else
            {
                result = input.Clone();
            }
        }

        return result;
    }
}

