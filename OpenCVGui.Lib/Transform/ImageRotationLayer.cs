using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Layer that rotates an image by a specified angle
/// </summary>
public class ImageRotationLayer : BaseLayer
{
    [Parameter(DisplayName = "Rotation Angle", Description = "Rotation angle in degrees (positive = clockwise)", Order = 0)]
    [ParameterRange(-360.0, 360.0, 1.0)]
    public double Angle { get; set; } = 0.0;

    [Parameter(DisplayName = "Interpolation", Description = "Interpolation method for rotation", Order = 1)]
    public InterpolationFlags Interpolation { get; set; } = InterpolationFlags.Linear;

    [Parameter(DisplayName = "Expand to Fit", Description = "If true, expands the output image to fit the rotated image", Order = 2)]
    public bool ExpandToFit { get; set; } = true;

    public override string GetLayerType() => "ImageRotation";

    protected override Mat RenderInternal(Mat? input)
    {
        if (input == null || input.Empty())
        {
            return new Mat();
        }

        var center = new Point2f(input.Width / 2.0f, input.Height / 2.0f);
        var rotationMatrix = Cv2.GetRotationMatrix2D(center, Angle, 1.0);

        Size outputSize;
        if (ExpandToFit)
        {
            // Calculate the bounding box of the rotated image
            var corners = new Point2f[]
            {
                new(0, 0),
                new(input.Width, 0),
                new(input.Width, input.Height),
                new(0, input.Height)
            };

            var rotatedCorners = corners.Select(corner =>
            {
                var point = new Mat(3, 1, MatType.CV_32FC1);
                point.Set<float>(0, 0, corner.X - center.X);
                point.Set<float>(1, 0, corner.Y - center.Y);
                point.Set<float>(2, 0, 1);

                var rotatedExpr = rotationMatrix * point;
                var rotated = new Mat(rotatedExpr);
                return new Point2f(rotated.Get<float>(0, 0) + center.X, rotated.Get<float>(1, 0) + center.Y);
            }).ToArray();

            var minX = rotatedCorners.Min(c => c.X);
            var maxX = rotatedCorners.Max(c => c.X);
            var minY = rotatedCorners.Min(c => c.Y);
            var maxY = rotatedCorners.Max(c => c.Y);

            outputSize = new Size((int)(maxX - minX), (int)(maxY - minY));
            rotationMatrix.Set<double>(0, 2, rotationMatrix.Get<double>(0, 2) - minX);
            rotationMatrix.Set<double>(1, 2, rotationMatrix.Get<double>(1, 2) - minY);
        }
        else
        {
            outputSize = new Size(input.Width, input.Height);
        }

        var result = new Mat();
        Cv2.WarpAffine(input, result, rotationMatrix, outputSize, Interpolation);
        return result;
    }
}

