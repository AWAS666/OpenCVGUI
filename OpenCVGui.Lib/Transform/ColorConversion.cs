using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

public class ColorConversion : BaseLayer
{
    [Parameter(DisplayName = "Conversion Code", Description = "Color space conversion code", Order = 0)]
    public ColorConversionCodes Target { get; set; } = ColorConversionCodes.BGR2HSV;

    public override string GetLayerType() => "ColorConversion";

    protected override Mat RenderInternal(Mat? input)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        var result = new Mat();
        Cv2.CvtColor(input, result, Target);
        return result;
    }
}
