using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Bitwise operation types
/// </summary>
public enum BitwiseOperation
{
    /// <summary>
    /// Bitwise AND: result = image1 & image2
    /// </summary>
    AND,

    /// <summary>
    /// Bitwise OR: result = image1 | image2
    /// </summary>
    OR,

    /// <summary>
    /// Bitwise XOR: result = image1 ^ image2
    /// </summary>
    XOR,

    /// <summary>
    /// Bitwise NOT: result = ~image (only uses first image)
    /// </summary>
    NOT
}

/// <summary>
/// Layer that performs bitwise operations on images
/// </summary>
public class BitwiseOperationsLayer : BaseLayer
{
    [Parameter(DisplayName = "Operation", Description = "Bitwise operation to perform", Order = 0)]
    public BitwiseOperation Operation { get; set; } = BitwiseOperation.AND;

    [Parameter(DisplayName = "Second Image Path", Description = "Path to the second image file (not needed for NOT operation)", Order = 1)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    public string SecondImagePath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Use Mask", Description = "Use a mask for the operation (only for AND, OR, XOR)", Order = 2)]
    public bool UseMask { get; set; } = false;

    [Parameter(DisplayName = "Mask Image Path", Description = "Path to the mask image file (grayscale, 0 = ignore, 255 = apply)", Order = 3)]
    [ParameterFile(Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif")]
    public string MaskImagePath { get; set; } = string.Empty;

    private Mat? _cachedSecondImage;
    private Mat? _cachedMask;
    private string _lastSecondImagePath = string.Empty;
    private string _lastMaskImagePath = string.Empty;

    public override string GetLayerType() => "BitwiseOperations";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        // For NOT operation, only need input image
        if (Operation == BitwiseOperation.NOT)
        {
            Mat notResult = new Mat();
            Cv2.BitwiseNot(input, notResult);
            return notResult;
        }

        // For other operations, need second image
        if (_cachedSecondImage == null || _lastSecondImagePath != SecondImagePath)
        {
            LoadSecondImage();
            _lastSecondImagePath = SecondImagePath ?? string.Empty;
        }

        if (_cachedSecondImage == null || _cachedSecondImage.Empty())
        {
            return input.Clone();
        }

        // Ensure images have same size
        Mat secondImage = _cachedSecondImage;
        bool needsCleanup = false;

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

        // Load mask if needed
        Mat? mask = null;
        if (UseMask)
        {
            if (_cachedMask == null || _lastMaskImagePath != MaskImagePath)
            {
                LoadMask();
                _lastMaskImagePath = MaskImagePath ?? string.Empty;
            }

            if (_cachedMask != null && !_cachedMask.Empty())
            {
                // Ensure mask matches input size
                if (_cachedMask.Width != input.Width || _cachedMask.Height != input.Height)
                {
                    Mat resizedMask = new Mat();
                    Cv2.Resize(_cachedMask, resizedMask, input.Size());
                    mask = resizedMask;
                }
                else
                {
                    mask = _cachedMask;
                }

                // Ensure mask is grayscale
                if (mask.Channels() != 1)
                {
                    Mat grayMask = new Mat();
                    Cv2.CvtColor(mask, grayMask, ColorConversionCodes.BGR2GRAY);
                    if (mask != _cachedMask)
                    {
                        mask.Dispose();
                    }
                    mask = grayMask;
                }
            }
        }

        // Perform bitwise operation
        Mat result = new Mat();
        if (mask != null)
        {
            switch (Operation)
            {
                case BitwiseOperation.AND:
                    Cv2.BitwiseAnd(input, secondImage, result, mask);
                    break;
                case BitwiseOperation.OR:
                    Cv2.BitwiseOr(input, secondImage, result, mask);
                    break;
                case BitwiseOperation.XOR:
                    Cv2.BitwiseXor(input, secondImage, result, mask);
                    break;
            }
        }
        else
        {
            switch (Operation)
            {
                case BitwiseOperation.AND:
                    Cv2.BitwiseAnd(input, secondImage, result);
                    break;
                case BitwiseOperation.OR:
                    Cv2.BitwiseOr(input, secondImage, result);
                    break;
                case BitwiseOperation.XOR:
                    Cv2.BitwiseXor(input, secondImage, result);
                    break;
            }
        }

        if (needsCleanup)
        {
            secondImage.Dispose();
        }

        if (mask != null && mask != _cachedMask)
        {
            mask.Dispose();
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

    private void LoadMask()
    {
        if (_cachedMask != null)
        {
            _cachedMask.Dispose();
            _cachedMask = null;
        }

        if (string.IsNullOrEmpty(MaskImagePath) || !File.Exists(MaskImagePath))
        {
            return;
        }

        try
        {
            _cachedMask = Cv2.ImRead(MaskImagePath, ImreadModes.Grayscale);
            if (_cachedMask == null || _cachedMask.Empty())
            {
                _cachedMask?.Dispose();
                _cachedMask = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading mask image: {ex.Message}");
            _cachedMask?.Dispose();
            _cachedMask = null;
        }
    }

    public void DisposeCachedImages()
    {
        _cachedSecondImage?.Dispose();
        _cachedSecondImage = null;
        _cachedMask?.Dispose();
        _cachedMask = null;
    }
}

