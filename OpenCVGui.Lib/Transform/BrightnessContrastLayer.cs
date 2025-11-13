using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that adjusts brightness and contrast of an image
/// </summary>
public class BrightnessContrastLayer : BaseLayer
{
    [Parameter(DisplayName = "Brightness", Description = "Brightness adjustment (-100 to 100, 0 = no change)", Order = 0)]
    [ParameterRange(-100.0, 100.0, 1.0)]
    public double Brightness { get; set; } = 0.0;

    [Parameter(DisplayName = "Contrast", Description = "Contrast adjustment (-100 to 100, 0 = no change)", Order = 1)]
    [ParameterRange(-100.0, 100.0, 1.0)]
    public double Contrast { get; set; } = 0.0;

    public override string GetLayerType() => "BrightnessContrast";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Convert contrast from -100..100 to multiplier (0.0..2.0)
        // Contrast of 0 = multiplier of 1.0, contrast of 100 = multiplier of 2.0, contrast of -100 = multiplier of 0.0
        double contrastMultiplier = 1.0 + (Contrast / 100.0);
        
        // Brightness is added directly (scaled to -255..255)
        double brightnessDelta = (Brightness / 100.0) * 255.0;

        // Apply transformation: output = input * contrast + brightness
        Mat result = new Mat();
        input.ConvertTo(result, -1, contrastMultiplier, brightnessDelta);

        return result;
    }
}

