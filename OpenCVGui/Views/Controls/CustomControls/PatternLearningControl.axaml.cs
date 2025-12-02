using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OpenCVGui.Services;
using OpenCVGui.ViewModels.Custom;

namespace OpenCVGui.Views.Controls.CustomControls;

public partial class PatternLearningControl : UserControl
{
    private bool _isDragging = false;
    private bool _isCropSelecting = false;
    private Point _lastMousePosition;
    private Point _cropStartPoint;

    public PatternLearningControl()
    {
        InitializeComponent();
        this.Loaded += PatternLearningControl_Loaded;
        this.DataContextChanged += PatternLearningControl_DataContextChanged;
    }

    private void PatternLearningControl_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetupViewModel();
        UpdateCursor();
        ClearCropSelection();
    }

    private void PatternLearningControl_DataContextChanged(object? sender, EventArgs e)
    {
        SetupViewModel();
    }

    private void SetupViewModel()
    {
        if (DataContext is PatternLearningControlViewModel viewModel)
        {
            if (WindowService.MainWindow?.DataContext is OpenCVGui.ViewModels.MainWindowViewModel mainViewModel)
            {
                viewModel.SetFileDialogService(mainViewModel.GetFileDialogService());
            }

            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(PatternLearningControlViewModel.IsCropMode))
                {
                    UpdateCursor();
                    if (!viewModel.IsCropMode)
                    {
                        ClearCropSelection();
                    }
                }
            };
        }
    }

    private void ClearCropSelection()
    {
        CropSelectionRectangle.IsVisible = false;
        CropSelectionRectangle.Width = 0;
        CropSelectionRectangle.Height = 0;
        _isCropSelecting = false;
    }

    private PatternLearningControlViewModel? ViewModel => DataContext as PatternLearningControlViewModel;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        if (ViewModel == null)
            return;

        var point = e.GetCurrentPoint(this);
        
        if (point.Properties.IsLeftButtonPressed)
        {
            if (ViewModel.IsCropMode)
            {
                // Start crop selection
                _isCropSelecting = true;
                _cropStartPoint = e.GetPosition(ImageContainer);
                CropSelectionRectangle.IsVisible = true;
                CropSelectionRectangle.Width = 0;
                CropSelectionRectangle.Height = 0;
                this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross);
                e.Handled = true;
            }
            else
            {
                // Normal pan mode
                _isDragging = true;
                _lastMousePosition = e.GetPosition(this);
                this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        
        if (ViewModel == null)
            return;

        if (_isCropSelecting)
        {
            // Update crop selection rectangle
            var currentPoint = e.GetPosition(ImageContainer);
            var startX = Math.Min(_cropStartPoint.X, currentPoint.X);
            var startY = Math.Min(_cropStartPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _cropStartPoint.X);
            var height = Math.Abs(currentPoint.Y - _cropStartPoint.Y);

            Canvas.SetLeft(CropSelectionRectangle, startX);
            Canvas.SetTop(CropSelectionRectangle, startY);
            CropSelectionRectangle.Width = width;
            CropSelectionRectangle.Height = height;
            e.Handled = true;
        }
        else if (_isDragging)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;
            
            ViewModel.PanImage(deltaX, deltaY);
            _lastMousePosition = currentPosition;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        if (ViewModel == null)
            return;

        if (_isCropSelecting)
        {
            // Finalize crop selection
            var endPoint = e.GetPosition(ImageContainer);
            var startX = Math.Min(_cropStartPoint.X, endPoint.X);
            var startY = Math.Min(_cropStartPoint.Y, endPoint.Y);
            var width = Math.Abs(endPoint.X - _cropStartPoint.X);
            var height = Math.Abs(endPoint.Y - _cropStartPoint.Y);

            if (width > 5 && height > 5 && ViewModel.DisplayImage != null)
            {
                // Convert display coordinates to image pixel coordinates
                var imageCoords = ConvertDisplayToImageCoordinates(startX, startY, width, height);
                if (imageCoords.HasValue)
                {
                    ViewModel.SetCropSelection(
                        imageCoords.Value.X,
                        imageCoords.Value.Y,
                        imageCoords.Value.Width,
                        imageCoords.Value.Height);
                }
            }

            _isCropSelecting = false;
            UpdateCursor();
            e.Handled = true;
        }
        else if (_isDragging)
        {
            _isDragging = false;
            UpdateCursor();
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        
        if (ViewModel != null && ViewModel.DisplayImage != null && !ViewModel.IsCropMode)
        {
            // Get position relative to the image control
            var position = e.GetPosition(DisplayImageControl);
            var delta = e.Delta.Y > 0 ? 1.0 : -1.0;
            
            ViewModel.ZoomAtPoint(delta, position.X, position.Y);
            e.Handled = true;
        }
    }

    private void UpdateCursor()
    {
        if (ViewModel == null)
        {
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
            return;
        }

        this.Cursor = ViewModel.IsCropMode
            ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross)
            : new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
    }

    private (int X, int Y, int Width, int Height)? ConvertDisplayToImageCoordinates(double displayX, double displayY, double displayWidth, double displayHeight)
    {
        if (ViewModel == null || ViewModel.DisplayImage == null)
            return null;

        try
        {
            // Get image dimensions in pixels
            var imageWidth = ViewModel.DisplayImage.PixelSize.Width;
            var imageHeight = ViewModel.DisplayImage.PixelSize.Height;

            // Get the image control's natural size (before transforms)
            var imageBounds = DisplayImageControl.Bounds;
            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
                return null;

            // Get the container bounds
            var containerBounds = ImageContainer.Bounds;
            
            // Get zoom and pan values
            var zoom = ViewModel.ZoomLevel;
            var panX = ViewModel.PanX;
            var panY = ViewModel.PanY;

            // The ImageBorder centers the Image, and the Image has RenderTransformOrigin="0.5,0.5"
            // So transforms are applied from the center of the image
            
            // Find the center of the container (where the image center is when not panned)
            var containerCenterX = containerBounds.Width / 2.0;
            var containerCenterY = containerBounds.Height / 2.0;
            
            // The image's center position after pan transform
            var imageCenterX = containerCenterX + panX;
            var imageCenterY = containerCenterY + panY;
            
            // The image's size after zoom (the actual displayed size)
            var scaledImageWidth = imageBounds.Width * zoom;
            var scaledImageHeight = imageBounds.Height * zoom;
            
            // The image's top-left corner in container coordinates
            var imageTopLeftX = imageCenterX - scaledImageWidth / 2.0;
            var imageTopLeftY = imageCenterY - scaledImageHeight / 2.0;
            
            // Convert display coordinates (relative to container) to coordinates relative to image's top-left
            var relativeToImageX = displayX - imageTopLeftX;
            var relativeToImageY = displayY - imageTopLeftY;
            
            // Now convert from scaled image coordinates to actual image pixel coordinates
            // Divide by zoom to get back to natural size, then scale to pixels
            var pixelX = (int)((relativeToImageX / zoom) * (imageWidth / imageBounds.Width));
            var pixelY = (int)((relativeToImageY / zoom) * (imageHeight / imageBounds.Height));
            var pixelWidth = (int)((displayWidth / zoom) * (imageWidth / imageBounds.Width));
            var pixelHeight = (int)((displayHeight / zoom) * (imageHeight / imageBounds.Height));
            
            // Clamp to image bounds
            pixelX = Math.Max(0, Math.Min(pixelX, imageWidth - 1));
            pixelY = Math.Max(0, Math.Min(pixelY, imageHeight - 1));
            pixelWidth = Math.Max(1, Math.Min(pixelWidth, imageWidth - pixelX));
            pixelHeight = Math.Max(1, Math.Min(pixelHeight, imageHeight - pixelY));
            
            return (pixelX, pixelY, pixelWidth, pixelHeight);
        }
        catch
        {
            return null;
        }
    }
}
