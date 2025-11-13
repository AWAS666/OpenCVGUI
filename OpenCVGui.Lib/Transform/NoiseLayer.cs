using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using System;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that adds noise to an image
/// </summary>
public class NoiseLayer : BaseLayer
{
    [Parameter(DisplayName = "Noise Type", Description = "Type of noise to add", Order = 0)]
    public NoiseType NoiseType { get; set; } = NoiseType.Gaussian;

    [Parameter(DisplayName = "Intensity", Description = "Noise intensity (standard deviation for Gaussian, probability for Salt and Pepper)", Order = 1)]
    [ParameterRange(0.0, 100.0, 0.1)]
    public double Intensity { get; set; } = 10.0;

    [Parameter(DisplayName = "Mean", Description = "Mean value for Gaussian noise (typically 0)", Order = 2)]
    [ParameterRange(-50.0, 50.0, 0.1)]
    public double Mean { get; set; } = 0.0;

    [Parameter(DisplayName = "Salt Probability", Description = "Probability of salt (white) noise (for Salt and Pepper)", Order = 3)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double SaltProbability { get; set; } = 0.05;

    [Parameter(DisplayName = "Pepper Probability", Description = "Probability of pepper (black) noise (for Salt and Pepper)", Order = 4)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double PepperProbability { get; set; } = 0.05;

    public override string GetLayerType() => "Noise";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat result = input.Clone();

        switch (NoiseType)
        {
            case NoiseType.Gaussian:
                AddGaussianNoise(result, Mean, Intensity);
                break;

            case NoiseType.SaltAndPepper:
                AddSaltAndPepperNoise(result, SaltProbability, PepperProbability);
                break;

            case NoiseType.Uniform:
                AddUniformNoise(result, Intensity);
                break;

            default:
                AddGaussianNoise(result, Mean, Intensity);
                break;
        }

        return result;
    }

    private static void AddGaussianNoise(Mat image, double mean, double stdDev)
    {
        Mat noise = new Mat(image.Size(), image.Type());
        Cv2.Randn(noise, new Scalar(mean), new Scalar(stdDev));
        
        Mat result = new Mat();
        Cv2.Add(image, noise, result);
        
        // Copy result back to image
        result.CopyTo(image);
        
        noise.Dispose();
        result.Dispose();
    }

    private static void AddSaltAndPepperNoise(Mat image, double saltProb, double pepperProb)
    {
        int channels = image.Channels();

        // Create random mask for salt and pepper
        Mat saltMask = new Mat(image.Size(), MatType.CV_8UC1);
        Mat pepperMask = new Mat(image.Size(), MatType.CV_8UC1);
        
        Cv2.Randu(saltMask, new Scalar(0), new Scalar(1.0));
        Cv2.Randu(pepperMask, new Scalar(0), new Scalar(1.0));
        
        Mat saltThreshold = new Mat();
        Mat pepperThreshold = new Mat();
        Cv2.Threshold(saltMask, saltThreshold, 1.0 - saltProb, 255, ThresholdTypes.Binary);
        Cv2.Threshold(pepperMask, pepperThreshold, 1.0 - pepperProb, 255, ThresholdTypes.Binary);
        
        // Apply salt (white) noise
        if (channels == 1)
        {
            image.SetTo(new Scalar(255), saltThreshold);
            image.SetTo(new Scalar(0), pepperThreshold);
        }
        else
        {
            Mat saltMaskBgr = new Mat();
            Cv2.CvtColor(saltThreshold, saltMaskBgr, ColorConversionCodes.GRAY2BGR);
            Mat pepperMaskBgr = new Mat();
            Cv2.CvtColor(pepperThreshold, pepperMaskBgr, ColorConversionCodes.GRAY2BGR);
            
            image.SetTo(new Scalar(255, 255, 255), saltMaskBgr);
            image.SetTo(new Scalar(0, 0, 0), pepperMaskBgr);
            
            saltMaskBgr.Dispose();
            pepperMaskBgr.Dispose();
        }
        
        saltMask.Dispose();
        pepperMask.Dispose();
        saltThreshold.Dispose();
        pepperThreshold.Dispose();
    }

    private static void AddUniformNoise(Mat image, double intensity)
    {
        Mat noise = new Mat(image.Size(), image.Type());
        Cv2.Randu(noise, new Scalar(-intensity), new Scalar(intensity));
        
        Mat result = new Mat();
        Cv2.Add(image, noise, result);
        
        // Copy result back to image
        result.CopyTo(image);
        
        noise.Dispose();
        result.Dispose();
    }
}

/// <summary>
/// Types of noise that can be added to images
/// </summary>
public enum NoiseType
{
    /// <summary>
    /// Gaussian noise - normally distributed noise
    /// </summary>
    Gaussian,

    /// <summary>
    /// Salt and pepper noise - random white and black pixels
    /// </summary>
    SaltAndPepper,

    /// <summary>
    /// Uniform noise - uniformly distributed noise
    /// </summary>
    Uniform
}

