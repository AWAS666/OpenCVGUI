using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

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
            // Calculate the bounding box of the rotated image corners
            var corners = new Point2f[]
            {
                new(0, 0),
                new(input.Width, 0),
                new(input.Width, input.Height),
                new(0, input.Height)
            };

            var rotatedCorners = new List<Point2f>();
            foreach (var corner in corners)
            {
                // Transform corner using rotation matrix
                var x = corner.X - center.X;
                var y = corner.Y - center.Y;
                
                // Apply rotation: [cos -sin] [x]   [sin cos] [y]
                var angleRad = Angle * Math.PI / 180.0;
                var cos = Math.Cos(angleRad);
                var sin = Math.Sin(angleRad);
                
                var rotatedX = x * cos - y * sin;
                var rotatedY = x * sin + y * cos;
                
                rotatedCorners.Add(new Point2f((float)(rotatedX + center.X), (float)(rotatedY + center.Y)));
            }

            var minX = rotatedCorners.Min(c => c.X);
            var maxX = rotatedCorners.Max(c => c.X);
            var minY = rotatedCorners.Min(c => c.Y);
            var maxY = rotatedCorners.Max(c => c.Y);

            outputSize = new Size((int)Math.Ceiling(maxX - minX), (int)Math.Ceiling(maxY - minY));
            
            // Adjust translation to account for the new bounding box
            var tx = rotationMatrix.Get<double>(0, 2);
            var ty = rotationMatrix.Get<double>(1, 2);
            rotationMatrix.Set<double>(0, 2, tx - minX);
            rotationMatrix.Set<double>(1, 2, ty - minY);
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

