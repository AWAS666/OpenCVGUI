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
    
    /// <summary>
    /// Gets the current frame position in the video
    /// </summary>
    public long CurrentFramePosition
    {
        get
        {
            if (_videoCapture != null && _videoCapture.IsOpened())
            {
                return (long)_videoCapture.Get(VideoCaptureProperties.PosFrames);
            }
            return 0;
        }
    }
    
    /// <summary>
    /// Gets the total number of frames in the video
    /// </summary>
    public long TotalFrames
    {
        get
        {
            if (_videoCapture != null && _videoCapture.IsOpened())
            {
                return (long)_videoCapture.Get(VideoCaptureProperties.FrameCount);
            }
            return 0;
        }
    }
    
    /// <summary>
    /// Steps forward one frame in the video
    /// </summary>
    public bool StepForward()
    {
        if (_videoCapture == null || !_videoCapture.IsOpened())
        {
            return false;
        }
        
        long currentFrame = CurrentFramePosition;
        long totalFrames = TotalFrames;
        
        if (currentFrame < totalFrames - 1)
        {
            _videoCapture.Set(VideoCaptureProperties.PosFrames, currentFrame + 1);
            return true;
        }
        else if (Loop)
        {
            // Loop to beginning
            _videoCapture.Set(VideoCaptureProperties.PosFrames, 0);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Steps backward one frame in the video
    /// </summary>
    public bool StepBackward()
    {
        if (_videoCapture == null || !_videoCapture.IsOpened())
        {
            return false;
        }
        
        long currentFrame = CurrentFramePosition;
        long totalFrames = TotalFrames;
        
        // PosFrames represents the next frame to be read
        // If we're at position N, the last frame read was N-1
        // To go back one frame, we need to set position to N-2 (to read frame N-2, then advance to N-1)
        // But we need to be careful: if currentFrame is 0, we're at the start
        // If currentFrame is 1, we want to go to frame 0, so set position to 0
        
        if (currentFrame > 1)
        {
            // Go back by setting position to currentFrame - 2
            // This will make the next read be frame (currentFrame - 2), then advance to (currentFrame - 1)
            _videoCapture.Set(VideoCaptureProperties.PosFrames, currentFrame - 2);
            return true;
        }
        else if (currentFrame == 1)
        {
            // We're at position 1, meaning we just read frame 0
            // To go back, we need to loop to the end if looping is enabled
            if (Loop && totalFrames > 0)
            {
                _videoCapture.Set(VideoCaptureProperties.PosFrames, totalFrames - 1);
                return true;
            }
            // If not looping, we're already at the beginning, can't go back
            return false;
        }
        else if (currentFrame == 0)
        {
            // We're at the very beginning
            if (Loop && totalFrames > 0)
            {
                // Loop to end
                _videoCapture.Set(VideoCaptureProperties.PosFrames, totalFrames - 1);
                return true;
            }
            return false;
        }
        
        return false;
    }

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

