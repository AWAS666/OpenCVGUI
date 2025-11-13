using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Import;

/// <summary>
/// Layer that captures images from a webcam. This is typically the first layer in a chain.
/// </summary>
public class WebcamLayer : BaseLayer
{
    private VideoCapture? _videoCapture;
    private int _lastDeviceIndex = -1;

    [Parameter(DisplayName = "Camera Device Index", Description = "Camera device index (0 = default camera, 1 = second camera, etc.)", Order = 0)]
    [ParameterRange(0, 10, 1)]
    public int DeviceIndex { get; set; } = 0;

    [Parameter(DisplayName = "Frame Width", Description = "Desired frame width (0 = use camera default)", Order = 1)]
    [ParameterRange(0, 7680, 1)]
    public int FrameWidth { get; set; } = 0;

    [Parameter(DisplayName = "Frame Height", Description = "Desired frame height (0 = use camera default)", Order = 2)]
    [ParameterRange(0, 4320, 1)]
    public int FrameHeight { get; set; } = 0;

    [Parameter(DisplayName = "Streaming", Description = "Enable continuous streaming from webcam", Order = 3)]
    public bool Streaming { get; set; } = true;

    [Parameter(DisplayName = "Target FPS", Description = "Target frames per second for streaming (0 = maximum)", Order = 4)]
    [ParameterRange(0, 120, 1)]
    public int TargetFps { get; set; } = 30;

    public override string GetLayerType() => "Webcam";

    protected override Mat RenderInternal(Mat? input)
    {
        // Initialize or reinitialize video capture if device index changed
        if (_videoCapture == null || _lastDeviceIndex != DeviceIndex)
        {
            ReleaseVideoCapture();
            
            _videoCapture = new VideoCapture(DeviceIndex);
            _lastDeviceIndex = DeviceIndex;

            if (!_videoCapture.IsOpened())
            {
                return new Mat();
            }

            // Set frame dimensions if specified
            if (FrameWidth > 0)
            {
                _videoCapture.Set(VideoCaptureProperties.FrameWidth, FrameWidth);
            }
            if (FrameHeight > 0)
            {
                _videoCapture.Set(VideoCaptureProperties.FrameHeight, FrameHeight);
            }
        }

        // Read frame from camera
        Mat frame = new Mat();
        if (!_videoCapture.Read(frame) || frame.Empty())
        {
            frame.Dispose();
            return new Mat();
        }

        return frame;
    }

    private void ReleaseVideoCapture()
    {
        if (_videoCapture != null)
        {
            _videoCapture.Release();
            _videoCapture.Dispose();
            _videoCapture = null;
        }
    }

    /// <summary>
    /// Cleanup resources when layer is disposed or removed
    /// </summary>
    ~WebcamLayer()
    {
        ReleaseVideoCapture();
    }
}

