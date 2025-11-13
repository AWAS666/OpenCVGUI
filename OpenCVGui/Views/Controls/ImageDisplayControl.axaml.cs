using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenCVGui.ViewModels;
using System;

namespace OpenCVGui.Views.Controls;

public partial class ImageDisplayControl : UserControl
{
    private bool _isDragging = false;
    private Point _lastMousePosition;
    private MainWindowViewModel? _viewModel;

    public ImageDisplayControl()
    {
        InitializeComponent();
        this.DataContextChanged += ImageDisplayControl_DataContextChanged;
    }

    private void ImageDisplayControl_DataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainWindowViewModel;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        
        if (_isDragging && _viewModel != null)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;
            
            _viewModel.PanImage(deltaX, deltaY);
            _lastMousePosition = currentPosition;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        
        if (_isDragging)
        {
            _isDragging = false;
            this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        
        if (_viewModel != null && _viewModel.DisplayImage != null)
        {
            // Get position relative to the image control
            var position = e.GetPosition(DisplayImageControl);
            var delta = e.Delta.Y > 0 ? 1.0 : -1.0;
            
            _viewModel.ZoomAtPoint(delta, position.X, position.Y);
            e.Handled = true;
        }
    }
}

