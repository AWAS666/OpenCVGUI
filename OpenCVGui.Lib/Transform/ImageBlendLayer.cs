using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that blends two images using alpha blending
/// </summary>
public class ImageBlendLayer : BaseLayer
{
    [Parameter(DisplayName = "Second Image Path", Description = "Path to the second image file to blend with", Order = 0)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    public string SecondImagePath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Alpha", Description = "Blending factor for first image (0.0 = only second image, 1.0 = only first image)", Order = 1)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double Alpha { get; set; } = 0.5;

    [Parameter(DisplayName = "Beta", Description = "Blending factor for second image (typically 1.0 - Alpha)", Order = 2)]
    [ParameterRange(0.0, 1.0, 0.01)]
    public double Beta { get; set; } = 0.5;

    [Parameter(DisplayName = "Gamma", Description = "Scalar added to each sum", Order = 3)]
    [ParameterRange(0.0, 100.0, 1.0)]
    public double Gamma { get; set; } = 0.0;

    private Mat? _cachedSecondImage;
    private string _lastSecondImagePath = string.Empty;

    public override string GetLayerType() => "ImageBlend";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // Load second image if path changed
        if (_cachedSecondImage == null || _lastSecondImagePath != SecondImagePath)
        {
            LoadSecondImage();
            _lastSecondImagePath = SecondImagePath ?? string.Empty;
        }

        if (_cachedSecondImage == null || _cachedSecondImage.Empty())
        {
            return input.Clone();
        }

        // Ensure images have same size and type
        Mat secondImage = _cachedSecondImage;
        Mat inputToUse = input;
        bool needsCleanup = false;

        // Resize second image to match input if needed
        if (secondImage.Width != input.Width || secondImage.Height != input.Height)
        {
            Mat resized = new Mat();
            Cv2.Resize(secondImage, resized, input.Size());
            if (needsCleanup && secondImage != _cachedSecondImage)
            {
                secondImage.Dispose();
            }
            secondImage = resized;
            needsCleanup = true;
        }

        // Ensure same number of channels
        if (input.Channels() != secondImage.Channels())
        {
            Mat converted = new Mat();
            if (input.Channels() == 1)
            {
                Cv2.CvtColor(secondImage, converted, ColorConversionCodes.BGR2GRAY);
            }
            else if (input.Channels() == 3)
            {
                if (secondImage.Channels() == 1)
                {
                    Cv2.CvtColor(secondImage, converted, ColorConversionCodes.GRAY2BGR);
                }
                else
                {
                    converted = secondImage.Clone();
                }
            }
            else
            {
                converted = secondImage.Clone();
            }

            if (needsCleanup)
            {
                secondImage.Dispose();
            }
            secondImage = converted;
            needsCleanup = true;
        }

        // Ensure same depth
        if (input.Depth() != secondImage.Depth())
        {
            Mat converted = new Mat();
            secondImage.ConvertTo(converted, input.Type());
            if (needsCleanup)
            {
                secondImage.Dispose();
            }
            secondImage = converted;
            needsCleanup = true;
        }

        // Perform alpha blending: result = alpha * input + beta * secondImage + gamma
        Mat result = new Mat();
        Cv2.AddWeighted(input, Alpha, secondImage, Beta, Gamma, result);

        if (needsCleanup)
        {
            secondImage.Dispose();
        }

        return result;
    }

    private void LoadSecondImage()
    {
        if (_cachedSecondImage != null)
        {
            _cachedSecondImage.Dispose();
            _cachedSecondImage = null;
        }

        if (string.IsNullOrEmpty(SecondImagePath) || !File.Exists(SecondImagePath))
        {
            return;
        }

        try
        {
            _cachedSecondImage = Cv2.ImRead(SecondImagePath, ImreadModes.Color);
            if (_cachedSecondImage == null || _cachedSecondImage.Empty())
            {
                _cachedSecondImage?.Dispose();
                _cachedSecondImage = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading second image: {ex.Message}");
            _cachedSecondImage?.Dispose();
            _cachedSecondImage = null;
        }
    }

    public void DisposeSecondImage()
    {
        _cachedSecondImage?.Dispose();
        _cachedSecondImage = null;
    }
}

