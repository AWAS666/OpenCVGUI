using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenCVGui.ViewModels;
using OpenCVGui.Utils;
using OpenCvSharp;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace OpenCVGui.Views;

public partial class HistogramWindow : Avalonia.Controls.Window
{
    private const int HistogramWidth = 800;
    private const int HistogramHeight = 500;
    private const int LeftPadding = 60;
    private const int RightPadding = 20;
    private const int TopPadding = 20;
    private const int BottomPadding = 60;
    private MainWindowViewModel? _mainViewModel;

    public HistogramWindow()
    {
        InitializeComponent();
        this.DataContextChanged += HistogramWindow_DataContextChanged;
        this.SizeChanged += HistogramWindow_SizeChanged;
        this.Loaded += HistogramWindow_Loaded;

        if (HistogramOverlay != null)
        {
            HistogramOverlay.PointerMoved += HistogramOverlay_PointerMoved;
            HistogramOverlay.PointerExited += HistogramOverlay_PointerExited;
        }
    }

    public void SetMainViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public void UpdateHistogram(Mat image)
    {
        if (DataContext is HistogramViewModel viewModel)
        {
            viewModel.CalculateHistogram(image);
            SubscribeToChannels(viewModel);
        }
    }

    private void HistogramWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DrawHistogram();
    }

    private void HistogramWindow_DataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is HistogramViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            SubscribeToChannels(viewModel);
        }
        DrawHistogram();
    }

    private void Channel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistogramChannel.IsVisible))
        {
            if (DataContext is HistogramViewModel viewModel)
            {
                var visibleChannels = viewModel.Channels.Where(c => c.IsVisible).ToList();
                if (visibleChannels.Count > 0)
                {
                    viewModel.MaxValue = visibleChannels.Max(c => c.Values.Max());
                }
                else
                {
                    viewModel.MaxValue = 0;
                }
            }
            DrawHistogram();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistogramViewModel.Channels))
        {
            if (DataContext is HistogramViewModel viewModel)
            {
                foreach (var channel in viewModel.Channels)
                {
                    channel.PropertyChanged -= Channel_PropertyChanged;
                    channel.PropertyChanged += Channel_PropertyChanged;
                }
            }
            DrawHistogram();
        }
        else if (e.PropertyName == nameof(HistogramViewModel.MaxValue) ||
                 e.PropertyName == nameof(HistogramViewModel.UseLogScale))
        {
            DrawHistogram();
        }
    }

    private void SubscribeToChannels(HistogramViewModel viewModel)
    {
        foreach (var channel in viewModel.Channels)
        {
            channel.PropertyChanged -= Channel_PropertyChanged;
            channel.PropertyChanged += Channel_PropertyChanged;
        }
    }

    private void HistogramWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        DrawHistogram();
    }

    private void DrawHistogram()
    {
        if (DataContext is not HistogramViewModel viewModel || HistogramImage == null)
            return;

        if (viewModel.Channels.Count == 0 || viewModel.MaxValue == 0)
        {
            HistogramImage.Source = null;
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

                foreach (var channel in viewModel.Channels)
                {
                    if (channel.Values.Length == 0 || !channel.IsVisible)
                        continue;

                    var bgrColor = ColorToBgr(channel.Color);
                    var channelColor = new Scalar(bgrColor.b, bgrColor.g, bgrColor.r);

                    double binWidth = (double)plotWidth / channel.Values.Length;

                    Point? prevPoint = null;
                    for (int i = 0; i < channel.Values.Length; i++)
                    {
                        int x = plotStartX + (int)(i * binWidth);
                        int y;

                        if (viewModel.UseLogScale && viewModel.MaxValue > 0)
                        {
                            double logValue = channel.Values[i] > 0
                                ? Math.Log10(channel.Values[i] + 1)
                                : 0;
                            double logMax = Math.Log10(viewModel.MaxValue + 1);
                            double normalizedValue = logMax > 0 ? logValue / logMax : 0;
                            y = plotEndY - (int)(normalizedValue * plotHeight);
                        }
                        else
                        {
                            double scaleY = (double)plotHeight / viewModel.MaxValue;
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

                DrawAxisLabels(histogramMat, plotStartX, plotStartY, plotEndX, plotEndY, plotWidth, plotHeight, viewModel, textColor);

                var bitmap = ImageConverter.MatToBitmapSafe(histogramMat);
                histogramMat.Dispose();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    HistogramImage.Source = bitmap;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing histogram: {ex.Message}");
            }
        });
    }

    private void DrawAxisLabels(Mat histogramMat, int plotStartX, int plotStartY, int plotEndX, int plotEndY,
        int plotWidth, int plotHeight, HistogramViewModel viewModel, Scalar textColor)
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

            if (viewModel.UseLogScale && viewModel.MaxValue > 0)
            {
                double ratio = (double)i / numYLabels;
                double logMax = Math.Log10(viewModel.MaxValue + 1);
                double logValue = logMax * ratio;
                value = (int)(Math.Pow(10, logValue) - 1);
                if (value < 0) value = 0;
            }
            else
            {
                value = (int)(viewModel.MaxValue * i / numYLabels);
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

    private (byte b, byte g, byte r) ColorToBgr(Color color)
    {
        return ((byte)color.B, (byte)color.G, (byte)color.R);
    }

    private void HistogramOverlay_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (DataContext is not HistogramViewModel viewModel || HistogramOverlay == null)
            return;

        if (viewModel.Channels.Count == 0 || viewModel.MaxValue == 0)
            return;

        var position = e.GetPosition(HistogramOverlay);

        if (position.X < 0 || position.X > HistogramWidth || position.Y < 0 || position.Y > HistogramHeight)
        {
            UpdateHoverDisplay(null, null, viewModel);
            return;
        }

        int plotWidth = HistogramWidth - LeftPadding - RightPadding;
        int plotHeight = HistogramHeight - TopPadding - BottomPadding;
        int plotStartX = LeftPadding;
        int plotStartY = TopPadding;
        int plotEndX = plotStartX + plotWidth;
        int plotEndY = plotStartY + plotHeight;

        if (position.X < plotStartX || position.X > plotEndX || position.Y < plotStartY || position.Y > plotEndY)
        {
            UpdateHoverDisplay(null, null, viewModel);
            return;
        }

        double plotX = position.X - plotStartX;
        double plotY = position.Y - plotStartY;

        if (viewModel.Channels.Count == 0 || viewModel.Channels[0].Values.Length == 0)
            return;

        int binCount = viewModel.Channels[0].Values.Length;
        int binIndex = (int)((plotX / plotWidth) * binCount);
        binIndex = Math.Max(0, Math.Min(binCount - 1, binIndex));

        int pixelIntensity = (int)((double)binIndex / binCount * 255);

        double normalizedY = 1.0 - (plotY / plotHeight);
        int frequencyValue;

        if (viewModel.UseLogScale && viewModel.MaxValue > 0)
        {
            double logMax = Math.Log10(viewModel.MaxValue + 1);
            double logValue = logMax * normalizedY;
            frequencyValue = (int)(Math.Pow(10, logValue) - 1);
            if (frequencyValue < 0) frequencyValue = 0;
        }
        else
        {
            frequencyValue = (int)(viewModel.MaxValue * normalizedY);
        }

        UpdateHoverDisplay(pixelIntensity, frequencyValue, viewModel, binIndex);
    }

    private void HistogramOverlay_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (HoverValueText != null)
        {
            HoverValueText.Text = "Hover over the chart to see values";
        }
    }

    private void UpdateHoverDisplay(int? pixelIntensity, int? frequencyValue, HistogramViewModel viewModel, int? binIndex = null)
    {
        if (HoverValueText == null)
            return;

        if (pixelIntensity == null || frequencyValue == null || binIndex == null)
        {
            HoverValueText.Text = "Hover over the chart to see values";
            return;
        }

        var text = new System.Text.StringBuilder();
        text.AppendLine($"Pixel Intensity: {pixelIntensity.Value}");
        text.AppendLine($"Y Position: {frequencyValue.Value}");
        text.AppendLine();
        text.AppendLine("Channel Values:");

        foreach (var channel in viewModel.Channels)
        {
            if (!channel.IsVisible || channel.Values.Length == 0)
                continue;

            if (binIndex.Value < channel.Values.Length)
            {
                int value = channel.Values[binIndex.Value];
                text.AppendLine($"  {channel.Name}: {value:N0}");
            }
        }

        HoverValueText.Text = text.ToString();
    }
}
