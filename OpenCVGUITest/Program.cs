using OpenCvSharp;
using System;
using System.Reflection.Emit;
using System.Threading.Channels;
using System.Threading.Tasks;



var sample = Cv2.ImRead("frame_0035.png", ImreadModes.Color);
var overlaid = sample.Clone();

//FindFilter(sample, overlaid);

var hsv = new Mat();
Cv2.CvtColor(sample, hsv, ColorConversionCodes.BGR2HSV);

//Scalar brownBgr = new Scalar(64, 123, 182); // BGR from #b67b40
Scalar brownBgr = new Scalar(14, 52, 105); // BGR from #69340e
Mat brownMat = new Mat(1, 1, MatType.CV_8UC3, brownBgr);
Mat brownHsvMat = new Mat();
Cv2.CvtColor(brownMat, brownHsvMat, ColorConversionCodes.BGR2HSV);

Vec3b brownHsv = brownHsvMat.At<Vec3b>(0, 0);

int tolH = 5;
int tolS = 50;
int tolV = 50;

Scalar lower = new Scalar(
    Math.Max(brownHsv.Item0 - tolH, 0),
    Math.Max(brownHsv.Item1 - tolS, 0),
    Math.Max(brownHsv.Item2 - tolV, 0)
);

Scalar upper = new Scalar(
    Math.Min(brownHsv.Item0 + tolH, 255),
    Math.Min(brownHsv.Item1 + tolS, 255),
    Math.Min(brownHsv.Item2 + tolV, 255)
);

Mat mask = new Mat();
Cv2.InRange(hsv, lower, upper, mask);

// Morphological cleanup
Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 1));
Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
Cv2.Erode(mask, mask, kernel, iterations: 1);
//Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);

//Cv2.ImShow("Brown Rectangles", mask);
//Cv2.WaitKey(0);

// Find contours
Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

foreach (var cnt in contours)
{
    var approx = Cv2.ApproxPolyDP(cnt, 0.02 * Cv2.ArcLength(cnt, true), true);
    if (approx.Length > 1 && Cv2.ContourArea(cnt) > 100)
    {
        var rect = Cv2.BoundingRect(cnt);
        Cv2.Rectangle(overlaid, rect, new Scalar(0, 255, 0), 1);
    }
}

Cv2.ImShow("Brown Rectangles", overlaid);
Cv2.WaitKey(0);
Cv2.DestroyAllWindows();


Cv2.ImWrite("output.png", overlaid);

static RotatedRect FindBox(int threshhold, int areaMin, Mat overlaid, Mat blurredChannel)
{
    int maxValue = 255;
    ThresholdTypes threshType = ThresholdTypes.Binary; // 0 == Binary
    Mat simpleThresh = new Mat(blurredChannel.Rows, blurredChannel.Cols, MatType.CV_8UC1);
    Cv2.Threshold(blurredChannel, simpleThresh, threshhold, maxValue, threshType);

    // Morphological operation
    int iterations = 1;
    MorphTypes operationType = MorphTypes.Open;
    Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(13, 1));
    Mat morph = new Mat(simpleThresh.Rows, simpleThresh.Cols, MatType.CV_8UC1);
    Cv2.MorphologyEx(simpleThresh, morph, operationType, kernel, iterations: iterations);

    // Connected Components with Stats
    int areaMax = 150_000;
    Mat labels = new Mat();
    Mat stats = new Mat();
    Mat centroids = new Mat();
    int numLabels = Cv2.ConnectedComponentsWithStats(morph, labels, stats, centroids);

    // Find first valid label and process it directly (early exit optimization)
    Mat mask = new Mat(labels.Rows, labels.Cols, MatType.CV_8UC1);
    for (int i = 1; i < numLabels; i++)
    {
        int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
        if (area > areaMin && area < areaMax)
        {
            // Early exit: Process only the first valid region
            Cv2.Compare(labels, i, mask, CmpType.EQ);

            // Draw contours and rectangles
            int thicknessRect = 2;
            Scalar textColor = new Scalar(255, 0, 255); // Magenta (BGR)

            try
            {
                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours.Length > 0)
                {
                    RotatedRect rect = Cv2.MinAreaRect(contours[0]);
                    Point2f[] box = rect.Points();
                    OpenCvSharp.Point[] intBox = Array.ConvertAll(box, p => new OpenCvSharp.Point((int)p.X, (int)p.Y));

                    Cv2.DrawContours(overlaid, new[] { intBox }, -1, textColor, thicknessRect);
                    Cv2.PutText(overlaid, $"{rect.Center.X - rect.Size.Width / 2}", new OpenCvSharp.Point(rect.Center.X - rect.Size.Width / 2, 75), HersheyFonts.HersheySimplex, 1, textColor);
                    Cv2.PutText(overlaid, $"{rect.Center.X + rect.Size.Width / 2}", new OpenCvSharp.Point(rect.Center.X + rect.Size.Width / 2 - 100, 75), HersheyFonts.HersheySimplex, 1, textColor);
                    //return rect;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
    return new();
}

static void FindFilter(Mat sample, Mat overlaid)
{
    int threshhold = 200;
    int areaMin = 500;
    Cv2.GaussianBlur(sample, sample, new Size(3, 3), 1.5);

    int channel = 1;
    Mat blurredChannel = new Mat(sample.Rows, sample.Cols, MatType.CV_8UC1);
    Cv2.ExtractChannel(sample, blurredChannel, channel);

    FindBox(threshhold, areaMin, overlaid, blurredChannel);
}