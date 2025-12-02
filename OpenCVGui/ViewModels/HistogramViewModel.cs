using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenCVGui.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenCVGui.ViewModels;

public partial class HistogramViewModel : ViewModelBase
{
    private const int HistogramWidth = 800;
    private const int HistogramHeight = 500;
    private const int LeftPadding = 60;
    private const int RightPadding = 20;
    private const int TopPadding = 20;
    private const int BottomPadding = 60;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistogramBitmap))]
    private List<HistogramChannel> _channels = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistogramBitmap))]
    private int _maxValue;

    [ObservableProperty]
    private int _binCount = 256;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistogramBitmap))]
    private bool _useLogScale = false;

    [ObservableProperty]
    private Bitmap? _histogramBitmap;

    [ObservableProperty]
    private string _hoverText = "Hover over the chart to see values";

    [ObservableProperty]
    private double _hoverX = -1;

    [ObservableProperty]
    private double _hoverY = -1;

    partial void OnUseLogScaleChanged(bool value)
    {
        RedrawHistogram();
    }

    partial void OnChannelsChanged(List<HistogramChannel> value)
    {
        SubscribeToChannelChanges();
        RedrawHistogram();
    }

    partial void OnMaxValueChanged(int value)
    {
        RedrawHistogram();
    }

    partial void OnHoverXChanged(double value)
    {
        UpdateHoverText();
    }

    partial void OnHoverYChanged(double value)
    {
        UpdateHoverText();
    }

    private void SubscribeToChannelChanges()
    {
        foreach (var channel in Channels)
        {
            channel.PropertyChanged -= Channel_PropertyChanged;
            channel.PropertyChanged += Channel_PropertyChanged;
        }
    }

    private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistogramChannel.IsVisible))
        {
            RecalculateMaxValue();
            RedrawHistogram();
        }
    }

    private void RecalculateMaxValue()
    {
        var visibleChannels = Channels.Where(c => c.IsVisible).ToList();
        MaxValue = visibleChannels.Count > 0 ? visibleChannels.Max(c => c.Values.Max()) : 0;
    }

    public void UpdateHoverPosition(double x, double y)
    {
        HoverX = x;
        HoverY = y;
    }

    public void ClearHover()
    {
        HoverX = -1;
        HoverY = -1;
        HoverText = "Hover over the chart to see values";
    }

    private void UpdateHoverText()
    {
        if (HoverX < 0 || HoverY < 0 || Channels.Count == 0 || MaxValue == 0)
        {
            HoverText = "Hover over the chart to see values";
            return;
        }

        int plotWidth = HistogramWidth - LeftPadding - RightPadding;
        int plotHeight = HistogramHeight - TopPadding - BottomPadding;
        int plotStartX = LeftPadding;
        int plotStartY = TopPadding;
        int plotEndX = plotStartX + plotWidth;
        int plotEndY = plotStartY + plotHeight;

        if (HoverX < plotStartX || HoverX > plotEndX || HoverY < plotStartY || HoverY > plotEndY)
        {
            HoverText = "Hover over the chart to see values";
            return;
        }

        double plotX = HoverX - plotStartX;
        double plotY = HoverY - plotStartY;

        if (Channels.Count == 0 || Channels[0].Values.Length == 0)
            return;

        int binCount = Channels[0].Values.Length;
        int binIndex = (int)((plotX / plotWidth) * binCount);
        binIndex = Math.Max(0, Math.Min(binCount - 1, binIndex));

        int pixelIntensity = (int)((double)binIndex / binCount * 255);

        double normalizedY = 1.0 - (plotY / plotHeight);
        int frequencyValue;

        if (UseLogScale && MaxValue > 0)
        {
            double logMax = Math.Log10(MaxValue + 1);
            double logValue = logMax * normalizedY;
            frequencyValue = (int)(Math.Pow(10, logValue) - 1);
            if (frequencyValue < 0) frequencyValue = 0;
        }
        else
        {
            frequencyValue = (int)(MaxValue * normalizedY);
        }

        var text = new System.Text.StringBuilder();
        text.AppendLine($"Pixel Intensity: {pixelIntensity}");
        text.AppendLine($"Y Position: {frequencyValue}");
        text.AppendLine();
        text.AppendLine("Channel Values:");

        foreach (var channel in Channels)
        {
            if (!channel.IsVisible || channel.Values.Length == 0)
                continue;

            if (binIndex < channel.Values.Length)
            {
                int value = channel.Values[binIndex];
                text.AppendLine($"  {channel.Name}: {value:N0}");
            }
        }

        HoverText = text.ToString();
    }

    public void CalculateHistogram(Mat? image)
    {
        var newChannels = new List<HistogramChannel>();

        if (image == null || image.Empty())
        {
            Channels = newChannels;
            MaxValue = 0;
            return;
        }

        try
        {
            if (image.Channels() == 1)
            {
                var histogram = CalculateChannelHistogram(image, 0);
                newChannels.Add(new HistogramChannel
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

                    newChannels.Add(new HistogramChannel { Name = "Blue", Values = bHist, Color = Colors.Blue, IsVisible = true });
                    newChannels.Add(new HistogramChannel { Name = "Green", Values = gHist, Color = Colors.Green, IsVisible = true });
                    newChannels.Add(new HistogramChannel { Name = "Red", Values = rHist, Color = Colors.Red, IsVisible = true });
                    newChannels.Add(new HistogramChannel { Name = "White", Values = whiteHist, Color = Colors.White, IsVisible = true });
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

                        newChannels.Add(new HistogramChannel { Name = "Blue", Values = bHist, Color = Colors.Blue, IsVisible = true });
                        newChannels.Add(new HistogramChannel { Name = "Green", Values = gHist, Color = Colors.Green, IsVisible = true });
                        newChannels.Add(new HistogramChannel { Name = "Red", Values = rHist, Color = Colors.Red, IsVisible = true });
                        newChannels.Add(new HistogramChannel { Name = "Alpha", Values = aHist, Color = Colors.White, IsVisible = true });
                        newChannels.Add(new HistogramChannel { Name = "White", Values = whiteHist, Color = Colors.LightGray, IsVisible = true });
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

            Channels = newChannels;
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
        MaxValue = visibleChannels.Count > 0 ? visibleChannels.Max(c => c.Values.Max()) : 0;
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

    public void RedrawHistogram()
    {
        if (Channels.Count == 0 || MaxValue == 0)
        {
            HistogramBitmap = null;
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var histogramMat = new Mat(HistogramHeight, HistogramWidth, MatType.CV_8UC3, new Scalar(30, 30, 30));

                int plotWidth = HistogramWidth - LeftPadding - RightPadding;
                int plotHeight = HistogramHeight - TopPadding - BottomPadding;
                int plotStartX = LeftPadding;
                int plotStartY = TopPadding;
                int plotEndX = plotStartX + plotWidth;
                int plotEndY = plotStartY + plotHeight;

                var axisColor = new Scalar(128, 128, 128);
                var textColor = new Scalar(200, 200, 200);

                Cv2.Line(histogramMat, new Point(plotStartX, plotEndY), new Point(plotEndX, plotEndY), axisColor, 2);
                Cv2.Line(histogramMat, new Point(plotStartX, plotStartY), new Point(plotStartX, plotEndY), axisColor, 2);

                foreach (var channel in Channels)
                {
                    if (channel.Values.Length == 0 || !channel.IsVisible)
                        continue;

                    var channelColor = new Scalar(channel.Color.B, channel.Color.G, channel.Color.R);
                    double binWidth = (double)plotWidth / channel.Values.Length;

                    Point? prevPoint = null;
                    for (int i = 0; i < channel.Values.Length; i++)
                    {
                        int x = plotStartX + (int)(i * binWidth);
                        int y;

                        if (UseLogScale && MaxValue > 0)
                        {
                            double logValue = channel.Values[i] > 0
                                ? Math.Log10(channel.Values[i] + 1)
                                : 0;
                            double logMax = Math.Log10(MaxValue + 1);
                            double normalizedValue = logMax > 0 ? logValue / logMax : 0;
                            y = plotEndY - (int)(normalizedValue * plotHeight);
                        }
                        else
                        {
                            double scaleY = (double)plotHeight / MaxValue;
                            y = plotEndY - (int)(channel.Values[i] * scaleY);
                        }

                        var currentPoint = new Point(x, y);
                        if (prevPoint.HasValue)
                        {
                            Cv2.Line(histogramMat, prevPoint.Value, currentPoint, channelColor, 2);
                        }
                        prevPoint = currentPoint;
                    }
                }

                DrawAxisLabels(histogramMat, plotStartX, plotStartY, plotEndX, plotEndY, plotWidth, plotHeight, textColor);

                var bitmap = ImageConverter.MatToBitmapSafe(histogramMat);
                histogramMat.Dispose();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    HistogramBitmap = bitmap;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing histogram: {ex.Message}");
            }
        });
    }

    private void DrawAxisLabels(Mat histogramMat, int plotStartX, int plotStartY, int plotEndX, int plotEndY,
        int plotWidth, int plotHeight, Scalar textColor)
    {
        const int numXLabels = 5;
        const int numYLabels = 5;

        for (int i = 0; i <= numXLabels; i++)
        {
            int x = plotStartX + (plotWidth * i / numXLabels);
            int value = (i * 255 / numXLabels);
            string label = value.ToString();

            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);
            int textX = x - textSize.Width / 2;
            int textY = plotEndY + textSize.Height + 5;

            Cv2.PutText(histogramMat, label, new Point(textX, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 1);

            if (i > 0 && i < numXLabels)
            {
                Cv2.Line(histogramMat, new Point(x, plotEndY), new Point(x, plotEndY + 5), textColor, 1);
            }
        }

        for (int i = 0; i <= numYLabels; i++)
        {
            int y = plotEndY - (plotHeight * i / numYLabels);
            int value;

            if (UseLogScale && MaxValue > 0)
            {
                double ratio = (double)i / numYLabels;
                double logMax = Math.Log10(MaxValue + 1);
                double logValue = logMax * ratio;
                value = (int)(Math.Pow(10, logValue) - 1);
                if (value < 0) value = 0;
            }
            else
            {
                value = (int)(MaxValue * i / numYLabels);
            }

            string label = value.ToString();

            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out int baseline);
            int textX = plotStartX - textSize.Width - 5;
            int textY = y + textSize.Height / 2;

            Cv2.PutText(histogramMat, label, new Point(textX, textY), HersheyFonts.HersheySimplex, 0.5, textColor, 1);

            if (i > 0 && i < numYLabels)
            {
                Cv2.Line(histogramMat, new Point(plotStartX, y), new Point(plotStartX - 5, y), textColor, 1);
            }
        }
    }
}

public partial class HistogramChannel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int[] _values = Array.Empty<int>();

    [ObservableProperty]
    private Color _color;

    [ObservableProperty]
    private bool _isVisible = true;
}
