using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Contour;

/// <summary>
/// Morphological operation types
/// </summary>
public enum MorphologyOperation
{
    /// <summary>
    /// Erosion - shrinks objects
    /// </summary>
    Erode,

    /// <summary>
    /// Dilation - expands objects
    /// </summary>
    Dilate,

    /// <summary>
    /// Opening - erosion followed by dilation (removes noise)
    /// </summary>
    Open,

    /// <summary>
    /// Closing - dilation followed by erosion (fills holes)
    /// </summary>
    Close,

    /// <summary>
    /// Morphological gradient - difference between dilation and erosion
    /// </summary>
    Gradient,

    /// <summary>
    /// Top hat - difference between input and opening
    /// </summary>
    TopHat,

    /// <summary>
    /// Black hat - difference between closing and input
    /// </summary>
    BlackHat
}

/// <summary>
/// Morphological shape options
/// </summary>
public enum MorphologyShape
{
    /// <summary>
    /// Rectangular kernel
    /// </summary>
    Rect,

    /// <summary>
    /// Elliptical kernel
    /// </summary>
    Ellipse,

    /// <summary>
    /// Cross-shaped kernel
    /// </summary>
    Cross
}

/// <summary>
/// Layer that performs morphological operations on an image
/// </summary>
public class MorphologyLayer : BaseLayer
{
    [Parameter(DisplayName = "Operation", Description = "Morphological operation to perform", Order = 0)]
    public MorphologyOperation Operation { get; set; } = MorphologyOperation.Open;

    [Parameter(DisplayName = "Kernel Size X", Description = "Width of the morphological kernel", Order = 1)]
    [ParameterRange(1, 50, 1)]
    public int KernelSizeX { get; set; } = 3;

    [Parameter(DisplayName = "Kernel Size Y", Description = "Height of the morphological kernel", Order = 2)]
    [ParameterRange(1, 50, 1)]
    public int KernelSizeY { get; set; } = 3;

    [Parameter(DisplayName = "Kernel Shape", Description = "Shape of the morphological kernel", Order = 3)]
    public MorphologyShape KernelShape { get; set; } = MorphologyShape.Rect;

    [Parameter(DisplayName = "Iterations", Description = "Number of times the operation is applied", Order = 4)]
    [ParameterRange(1, 10, 1)]
    public int Iterations { get; set; } = 1;

    public override string GetLayerType() => "Morphology";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        Mat mask = new Mat();
        if (input.Channels() == 3)
        {
            Cv2.CvtColor(input, mask, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            mask = input.Clone();
        }

        MorphShapes shape = KernelShape switch
        {
            MorphologyShape.Rect => MorphShapes.Rect,
            MorphologyShape.Ellipse => MorphShapes.Ellipse,
            MorphologyShape.Cross => MorphShapes.Cross,
            _ => MorphShapes.Rect
        };

        Mat kernel = Cv2.GetStructuringElement(shape, new Size(KernelSizeX, KernelSizeY));
        Mat result = new Mat();

        MorphTypes morphType = Operation switch
        {
            MorphologyOperation.Erode => MorphTypes.Erode,
            MorphologyOperation.Dilate => MorphTypes.Dilate,
            MorphologyOperation.Open => MorphTypes.Open,
            MorphologyOperation.Close => MorphTypes.Close,
            MorphologyOperation.Gradient => MorphTypes.Gradient,
            MorphologyOperation.TopHat => MorphTypes.TopHat,
            MorphologyOperation.BlackHat => MorphTypes.BlackHat,
            _ => MorphTypes.Open
        };

        if (Operation == MorphologyOperation.Erode || Operation == MorphologyOperation.Dilate)
        {
            if (Operation == MorphologyOperation.Erode)
            {
                Cv2.Erode(mask, result, kernel, iterations: Iterations);
            }
            else
            {
                Cv2.Dilate(mask, result, kernel, iterations: Iterations);
            }
        }
        else
        {
            Cv2.MorphologyEx(mask, result, morphType, kernel, iterations: Iterations);
        }

        mask.Dispose();
        kernel.Dispose();

        return result;
    }
}

