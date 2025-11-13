using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OpenCVGui.ViewModels;

namespace OpenCVGui.Views.Controls;

public partial class LayerListControl : UserControl
{
    private Point _dragStartPoint;
    private LayerViewModel? _draggedItem;
    private int _draggedIndex = -1;
    private bool _isDragging = false;
    private const double MinimumDragDistance = 10.0;

    public LayerListControl()
    {
        InitializeComponent();
        DataContextChanged += LayerListControl_DataContextChanged;
    }

    private void LayerListControl_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.SelectedLayer))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (LayerListBox != null && LayerListBox.IsVisible)
                        {
                            LayerListBox.Focus();
                        }
                    }, Avalonia.Threading.DispatcherPriority.Input);
                }
            };
        }
    }

    private void LayerItemBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is CheckBox)
            return;

        if (sender is not Control control || control.DataContext is not LayerViewModel layerViewModel)
            return;

        // Capture pointer to this control to track movement
        e.Pointer.Capture(control);
        
        _dragStartPoint = e.GetPosition(control);
        _draggedItem = layerViewModel;
        _draggedIndex = DataContext is MainWindowViewModel vm ? vm.Layers.IndexOf(layerViewModel) : -1;
        _isDragging = false;
    }

    private async void LayerItemBorder_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedItem == null || _draggedIndex < 0 || _isDragging)
            return;

        if (sender is not Control control)
            return;

        if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            var mousePos = e.GetPosition(control);
            var diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) > MinimumDragDistance || Math.Abs(diff.Y) > MinimumDragDistance)
            {
                _isDragging = true;
                
                // Create DataTransfer - we track the item ourselves in _draggedItem
                var dataTransfer = new DataTransfer();
                
                var result = await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Move);
                
                // Release pointer capture
                e.Pointer.Capture(null);
                
                if (result == DragDropEffects.None)
                {
                    _draggedItem = null;
                    _draggedIndex = -1;
                }
                
                _isDragging = false;
            }
        }
    }

    private void LayerItemBorder_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
            return;

        // Release pointer capture if we haven't started dragging
        if (!_isDragging)
        {
            e.Pointer.Capture(null);
            _draggedItem = null;
            _draggedIndex = -1;
        }
    }

    private void LayerListBox_DragOver(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (sender is not ListBox listBox)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        // Use hit testing to find the ListBoxItem under the cursor
        var listBoxItem = GetListBoxItemAtPosition(e, listBox);
        
        if (listBoxItem?.DataContext is LayerViewModel)
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void LayerListBox_Drop(object? sender, DragEventArgs e)
    {
        if (_draggedItem == null || _draggedIndex < 0)
            return;

        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (sender is not ListBox listBox)
            return;

        // Use hit testing to find the ListBoxItem under the cursor
        var listBoxItem = GetListBoxItemAtPosition(e, listBox);
        if (listBoxItem?.DataContext is not LayerViewModel targetData)
            return;

        var targetIndex = viewModel.Layers.IndexOf(targetData);
        if (targetIndex < 0 || targetIndex == _draggedIndex)
            return;

        viewModel.MoveLayer(_draggedIndex, targetIndex);
        
        _draggedItem = null;
        _draggedIndex = -1;
        _isDragging = false;
    }

    private static ListBoxItem? GetListBoxItemAtPosition(DragEventArgs e, ListBox listBox)
    {
        if (listBox == null)
            return null;

        // Get the position relative to the ListBox
        var position = e.GetPosition(listBox);
        
        // Use hit testing to find the visual at this position
        var hit = listBox.InputHitTest(position);
        if (hit == null)
            return null;

        // Walk up the visual tree to find the ListBoxItem
        var current = hit as Visual;
        while (current != null)
        {
            if (current is ListBoxItem item)
                return item;
            
            current = current.GetVisualParent();
        }

        return null;
    }
}

