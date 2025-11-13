using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Arithmetic operation types
/// </summary>
public enum ArithmeticOperation
{
    /// <summary>
    /// Add two images: result = image1 + image2
    /// </summary>
    Add,

    /// <summary>
    /// Subtract two images: result = image1 - image2
    /// </summary>
    Subtract,

    /// <summary>
    /// Multiply two images: result = image1 * image2
    /// </summary>
    Multiply,

    /// <summary>
    /// Divide two images: result = image1 / image2
    /// </summary>
    Divide
}

/// <summary>
/// Layer that performs arithmetic operations between two images
/// </summary>
public class ImageArithmeticLayer : BaseLayer
{
    [Parameter(DisplayName = "Operation", Description = "Arithmetic operation to perform", Order = 0)]
    public ArithmeticOperation Operation { get; set; } = ArithmeticOperation.Add;

    [Parameter(DisplayName = "Second Image Path", Description = "Path to the second image file", Order = 1)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    public string SecondImagePath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Scale", Description = "Optional scale factor for the result", Order = 2)]
    [ParameterRange(0.1, 10.0, 0.1)]
    public double Scale { get; set; } = 1.0;

    private Mat? _cachedSecondImage;
    private string _lastSecondImagePath = string.Empty;

    public override string GetLayerType() => "ImageArithmetic";

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

        // Perform arithmetic operation
        Mat result = new Mat();
        switch (Operation)
        {
            case ArithmeticOperation.Add:
                Cv2.Add(input, secondImage, result);
                break;
            case ArithmeticOperation.Subtract:
                Cv2.Subtract(input, secondImage, result);
                break;
            case ArithmeticOperation.Multiply:
                Cv2.Multiply(input, secondImage, result, Scale);
                break;
            case ArithmeticOperation.Divide:
                Cv2.Divide(input, secondImage, result, Scale);
                break;
        }

        // Apply scale if needed (for add/subtract)
        if (Scale != 1.0 && (Operation == ArithmeticOperation.Add || Operation == ArithmeticOperation.Subtract))
        {
            Mat scaled = new Mat();
            result.ConvertTo(scaled, -1, Scale);
            result.Dispose();
            result = scaled;
        }

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

