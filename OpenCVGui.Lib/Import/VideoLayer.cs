using OpenCvSharp;
using OpenCVGui.Lib.Attributes;

namespace OpenCVGui.Lib.Import;

/// <summary>
/// Layer that streams frames from an MP4 video file. This is typically the first layer in a chain.
/// </summary>
public class VideoLayer : BaseLayer
{
    private VideoCapture? _videoCapture;
    private string _lastVideoPath = string.Empty;

    [Parameter(DisplayName = "Video Path", Description = "Path to the MP4 video file to load", Order = 0)]
    [ParameterFile(Filter = "Video files|*.mp4;*.avi;*.mov;*.mkv;*.wmv")]
    public string VideoPath { get; set; } = string.Empty;

    [Parameter(DisplayName = "Streaming", Description = "Enable continuous streaming from video", Order = 1)]
    public bool Streaming { get; set; } = true;

    [Parameter(DisplayName = "Loop", Description = "Loop the video when it reaches the end", Order = 2)]
    public bool Loop { get; set; } = true;

    [Parameter(DisplayName = "Target FPS", Description = "Target frames per second for streaming (0 = use video FPS)", Order = 3)]
    [ParameterRange(0, 120, 1)]
    public int TargetFps { get; set; } = 0;
    
    private double _videoFps = 30.0;
    
    /// <summary>
    /// Gets the native FPS of the video file
    /// </summary>
    public double VideoFps => _videoFps;

    public override string GetLayerType() => "Video";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        if (string.IsNullOrEmpty(VideoPath) || !File.Exists(VideoPath))
        {
            return new Mat();
        }

        // Initialize or reinitialize video capture if path changed
        if (_videoCapture == null || _lastVideoPath != VideoPath)
        {
            ReleaseVideoCapture();
            
            _videoCapture = new VideoCapture(VideoPath);
            _lastVideoPath = VideoPath;

            if (!_videoCapture.IsOpened())
            {
                return new Mat();
            }
            
            // Get video FPS
            _videoFps = _videoCapture.Get(VideoCaptureProperties.Fps);
            if (_videoFps <= 0)
            {
                _videoFps = 30.0; // Default fallback
            }
        }

        // Read frame from video
        Mat frame = new Mat();
        if (!_videoCapture.Read(frame) || frame.Empty())
        {
            frame.Dispose();
            
            // If looping is enabled and we've reached the end, restart
            if (Loop)
            {
                _videoCapture.Set(VideoCaptureProperties.PosFrames, 0);
                if (_videoCapture.Read(frame) && !frame.Empty())
                {
                    return frame;
                }
                frame.Dispose();
            }
            
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
    ~VideoLayer()
    {
        ReleaseVideoCapture();
    }
}

