using System;
using System.IO;
using OpenCVGui.Lib;
using OpenCVGui.ViewModels;
using OpenCvSharp;

namespace OpenCVGui.Services;

public class CropService
{
    public ImageCropData? CreateCropData(Mat sourceMat, int x, int y, int width, int height)
    {
        if (sourceMat == null || sourceMat.Empty())
            return null;

        var imgWidth = sourceMat.Width;
        var imgHeight = sourceMat.Height;

        x = Math.Max(0, Math.Min(x, imgWidth - 1));
        y = Math.Max(0, Math.Min(y, imgHeight - 1));
        width = Math.Max(1, Math.Min(width, imgWidth - x));
        height = Math.Max(1, Math.Min(height, imgHeight - y));

        var rect = new Rect(x, y, width, height);
        var cropped = new Mat(sourceMat, rect);

        return new ImageCropData(cropped.Clone(), x, y, width, height);
    }

    public void ApplyCropToParameter(ParameterProperty parameter, ImageCropData cropData)
    {
        if (parameter == null || cropData == null)
            return;

        if (parameter.PropertyType == typeof(Mat))
        {
            parameter.Value = cropData.CroppedImage.Clone();
        }
        else if (parameter.PropertyType == typeof(byte[]))
        {
            var encoded = cropData.CroppedImage.ImEncode(".png");
            parameter.Value = encoded;
        }
        else if (parameter.PropertyType == typeof(string))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"crop_{Guid.NewGuid()}.png");
            Cv2.ImWrite(tempPath, cropData.CroppedImage);
            parameter.Value = tempPath;
        }
    }
}

