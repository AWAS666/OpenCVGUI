using OpenCvSharp;
using OpenCVGui.Lib.Attributes;
using System;

namespace OpenCVGui.Lib.Import;

/// <summary>
/// Layer that streams frames from an MP4 video file. This is typically the first layer in a chain.
/// </summary>
public class VideoLayer : BaseLayer
{
    private VideoCapture? _videoCapture;
    private string _lastVideoPath = string.Empty;
    private readonly object _videoCaptureLock = new object();

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
            try
            {
                lock (_videoCaptureLock)
                {
                    if (_videoCapture != null && _videoCapture.IsOpened())
                    {
                        return (long)_videoCapture.Get(VideoCaptureProperties.PosFrames);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current frame position: {ex.Message}");
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
            try
            {
                lock (_videoCaptureLock)
                {
                    if (_videoCapture != null && _videoCapture.IsOpened())
                    {
                        return (long)_videoCapture.Get(VideoCaptureProperties.FrameCount);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting total frames: {ex.Message}");
            }
            return 0;
        }
    }
    
    /// <summary>
    /// Steps forward one frame in the video
    /// </summary>
    public bool StepForward()
    {
        try
        {
            lock (_videoCaptureLock)
            {
                if (_videoCapture == null || !_videoCapture.IsOpened())
                {
                    return false;
                }
                
                long currentFrame = (long)_videoCapture.Get(VideoCaptureProperties.PosFrames);
                long totalFrames = (long)_videoCapture.Get(VideoCaptureProperties.FrameCount);
                
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
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stepping forward: {ex.Message}");
        }
        
        return false;
    }
    
    /// <summary>
    /// Steps backward one frame in the video
    /// </summary>
    public bool StepBackward()
    {
        try
        {
            lock (_videoCaptureLock)
            {
                if (_videoCapture == null || !_videoCapture.IsOpened())
                {
                    return false;
                }
                
                long currentFrame = (long)_videoCapture.Get(VideoCaptureProperties.PosFrames);
                long totalFrames = (long)_videoCapture.Get(VideoCaptureProperties.FrameCount);
                
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
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stepping backward: {ex.Message}");
        }
        
        return false;
    }

    public override string GetLayerType() => "Video";

    protected override Mat RenderInternal(Mat? input, RenderMode renderMode, Mat? chainInput = null)
    {
        try
        {
            if (string.IsNullOrEmpty(VideoPath) || !File.Exists(VideoPath))
            {
                return new Mat();
            }

            lock (_videoCaptureLock)
            {
                // Initialize or reinitialize video capture if path changed
                if (_videoCapture == null || _lastVideoPath != VideoPath)
                {
                    ReleaseVideoCapture();
                    
                    try
                    {
                        _videoCapture = new VideoCapture(VideoPath);
                        _lastVideoPath = VideoPath;

                        if (!_videoCapture.IsOpened())
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to open video: {VideoPath}");
                            return new Mat();
                        }
                        
                        // Get video FPS
                        _videoFps = _videoCapture.Get(VideoCaptureProperties.Fps);
                        if (_videoFps <= 0)
                        {
                            _videoFps = 30.0; // Default fallback
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error initializing video capture: {ex.Message}");
                        ReleaseVideoCapture();
                        return new Mat();
                    }
                }

                // Read frame from video
                Mat frame = new Mat();
                try
                {
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
                catch (Exception ex)
                {
                    frame?.Dispose();
                    System.Diagnostics.Debug.WriteLine($"Error reading video frame: {ex.Message}");
                    return new Mat();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in RenderInternal: {ex.Message}");
            return new Mat();
        }
    }

    private void ReleaseVideoCapture()
    {
        lock (_videoCaptureLock)
        {
            if (_videoCapture != null)
            {
                try
                {
                    _videoCapture.Release();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error releasing video capture: {ex.Message}");
                }
                
                try
                {
                    _videoCapture.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing video capture: {ex.Message}");
                }
                
                _videoCapture = null;
            }
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

