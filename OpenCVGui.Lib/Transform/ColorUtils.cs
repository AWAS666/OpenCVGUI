using System;

namespace OpenCVGui.Lib.Transform;

/// <summary>
/// Utility class for color conversion operations
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Converts a hex color string to BGR tuple for OpenCV
    /// </summary>
    /// <param name="hex">Hex color string in format #RRGGBB or RRGGBB</param>
    /// <returns>Tuple of (B, G, R) values as bytes</returns>
    public static (byte b, byte g, byte r) HexToBgr(string hex)
    {
        // Remove # if present
        hex = hex.TrimStart('#');
        
        // Default to white if invalid
        if (hex.Length != 6)
        {
            return (255, 255, 255);
        }

        try
        {
            // Parse hex string (RRGGBB format)
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            
            // Return in BGR format for OpenCV
            return ((byte)b, (byte)g, (byte)r);
        }
        catch
        {
            return (255, 255, 255);
        }
    }
}

