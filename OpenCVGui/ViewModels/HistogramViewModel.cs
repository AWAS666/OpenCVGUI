using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCVGui.ViewModels;

public partial class HistogramViewModel : ViewModelBase
{
    [ObservableProperty]
    private List<HistogramChannel> _channels = new();

    [ObservableProperty]
    private int _maxValue;

    [ObservableProperty]
    private int _binCount = 256;

    [ObservableProperty]
    private bool _useLogScale = false;

    partial void OnUseLogScaleChanged(bool value)
    {
        OnPropertyChanged(nameof(MaxValue));
    }

    public void CalculateHistogram(Mat? image)
    {
        Channels.Clear();
        MaxValue = 0;

        if (image == null || image.Empty())
        {
            return;
        }

        try
        {
            if (image.Channels() == 1)
            {
                var histogram = CalculateChannelHistogram(image, 0);
                Channels.Add(new HistogramChannel
                {
                    Name = "Grayscale",
                    Values = histogram,
                    Color = Colors.Gray,
                    IsVisible = true
                });
            }
            else if (image.Channels() == 3)
            {
                Mat[] channels = Cv2.Split(image);
                Mat gray = new Mat();
                try
                {
                    Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                    
                    var bHist = CalculateChannelHistogram(channels[0], 0);
                    var gHist = CalculateChannelHistogram(channels[1], 0);
                    var rHist = CalculateChannelHistogram(channels[2], 0);
                    var whiteHist = CalculateChannelHistogram(gray, 0);

                    Channels.Add(new HistogramChannel
                    {
                        Name = "Blue",
                        Values = bHist,
                        Color = Colors.Blue,
                        IsVisible = true
                    });
                    Channels.Add(new HistogramChannel
                    {
                        Name = "Green",
                        Values = gHist,
                        Color = Colors.Green,
                        IsVisible = true
                    });
                    Channels.Add(new HistogramChannel
                    {
                        Name = "Red",
                        Values = rHist,
                        Color = Colors.Red,
                        IsVisible = true
                    });
                    Channels.Add(new HistogramChannel
                    {
                        Name = "White",
                        Values = whiteHist,
                        Color = Colors.White,
                        IsVisible = true
                    });
                }
                finally
                {
                    foreach (var channel in channels)
                    {
                        channel.Dispose();
                    }
                    gray.Dispose();
                }
            }
            else if (image.Channels() == 4)
            {
                Mat[] channels = Cv2.Split(image);
                try
                {
                    var bHist = CalculateChannelHistogram(channels[0], 0);
                    var gHist = CalculateChannelHistogram(channels[1], 0);
                    var rHist = CalculateChannelHistogram(channels[2], 0);
                    var aHist = CalculateChannelHistogram(channels[3], 0);

                    Mat gray = new Mat();
                    try
                    {
                        Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
                        var whiteHist = CalculateChannelHistogram(gray, 0);
                        
                        Channels.Add(new HistogramChannel
                        {
                            Name = "Blue",
                            Values = bHist,
                            Color = Colors.Blue,
                            IsVisible = true
                        });
                        Channels.Add(new HistogramChannel
                        {
                            Name = "Green",
                            Values = gHist,
                            Color = Colors.Green,
                            IsVisible = true
                        });
                        Channels.Add(new HistogramChannel
                        {
                            Name = "Red",
                            Values = rHist,
                            Color = Colors.Red,
                            IsVisible = true
                        });
                        Channels.Add(new HistogramChannel
                        {
                            Name = "Alpha",
                            Values = aHist,
                            Color = Colors.White,
                            IsVisible = true
                        });
                        Channels.Add(new HistogramChannel
                        {
                            Name = "White",
                            Values = whiteHist,
                            Color = Colors.LightGray,
                            IsVisible = true
                        });
                    }
                    finally
                    {
                        gray.Dispose();
                    }
                }
                finally
                {
                    foreach (var channel in channels)
                    {
                        channel.Dispose();
                    }
                }
            }

            UpdateMaxValue();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error calculating histogram: {ex.Message}");
        }
    }

    private void UpdateMaxValue()
    {
        var visibleChannels = Channels.Where(c => c.IsVisible).ToList();
        if (visibleChannels.Count > 0)
        {
            MaxValue = visibleChannels.Max(c => c.Values.Max());
        }
        else
        {
            MaxValue = 0;
        }
    }

    private int[] CalculateChannelHistogram(Mat channel, int channelIndex)
    {
        int[] histSize = { BinCount };
        Rangef[] ranges = { new Rangef(0, 256) };
        Mat hist = new Mat();
        
        try
        {
            Cv2.CalcHist(new[] { channel }, new[] { channelIndex }, null, hist, 1, histSize, ranges);
            
            int[] result = new int[BinCount];
            for (int i = 0; i < BinCount; i++)
            {
                result[i] = (int)hist.Get<float>(i, 0);
            }
            
            return result;
        }
        finally
        {
            hist.Dispose();
        }
    }
}

public class HistogramChannel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isVisible = true;

    public string Name { get; set; } = string.Empty;
    public int[] Values { get; set; } = Array.Empty<int>();
    public Color Color { get; set; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

