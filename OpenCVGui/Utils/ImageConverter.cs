using Avalonia.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.IO;

namespace OpenCVGui.Utils;

/// <summary>
/// Utility class for converting between OpenCV Mat and Avalonia Bitmap
/// </summary>
public static class ImageConverter
{
    /// <summary>
    /// Converts an OpenCV Mat to an Avalonia Bitmap
    /// </summary>
    public static Bitmap? MatToBitmap(Mat? mat)
    {
        if (mat == null || mat.Empty())
        {
            return null;
        }

        try
        {
            // Convert Mat to System.Drawing.Bitmap first
            using var bitmap = mat.ToBitmap();
            
            // Convert to Avalonia Bitmap via memory stream
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;
            
            return new Bitmap(memoryStream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts an OpenCV Mat to an Avalonia Bitmap, handling different channel formats
    /// </summary>
    public static Bitmap? MatToBitmapSafe(Mat? mat)
    {
        if (mat == null || mat.Empty())
        {
            return null;
        }

        try
        {
            Mat? convertedMat = null;
            try
            {
                // Ensure we have a BGR image for conversion
                if (mat.Channels() == 1)
                {
                    // Grayscale - convert to BGR
                    convertedMat = new Mat();
                    Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.GRAY2BGR);
                }
                else if (mat.Channels() == 4)
                {
                    // BGRA - convert to BGR
                    convertedMat = new Mat();
                    Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.BGRA2BGR);
                }
                else if (mat.Channels() != 3)
                {
                    // Unknown format, use as-is (will fail conversion but that's okay)
                    convertedMat = mat;
                }
                else
                {
                    convertedMat = mat;
                }

                using var bitmap = convertedMat.ToBitmap();
                using var memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                memoryStream.Position = 0;
                
                return new Bitmap(memoryStream);
            }
            finally
            {
                // Dispose converted mat if we created one
                if (convertedMat != null && convertedMat != mat)
                {
                    convertedMat.Dispose();
                }
            }
        }
        catch
        {
            return null;
        }
    }
}

