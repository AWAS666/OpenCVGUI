using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCVGui.Lib.Transform;

public class ColorConversion : BaseLayer
{
    public ColorConversionCodes Target { get; set; } = ColorConversionCodes.BGR2HSV;

    protected override Mat RenderInternal(Mat? input)
    {
        var hsv = new Mat();
        Cv2.CvtColor(input, hsv, Target);
        return hsv;
    }
}
