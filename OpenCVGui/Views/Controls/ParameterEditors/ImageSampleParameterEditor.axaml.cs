using Avalonia.Controls;
using OpenCVGui.ViewModels;
using System;

namespace OpenCVGui.Views.Controls.ParameterEditors;

public partial class ImageSampleParameterEditor : UserControl
{
    public ImageSampleParameterEditor()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? FindMainWindowViewModel()
    {
        var control = this.Parent;
        while (control != null)
        {
            if (control is Control ctrl && ctrl.DataContext is MainWindowViewModel vm)
                return vm;
            
            control = control is Control c ? c.Parent : null;
        }
        
        return null;
    }

    private void CropFromImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ParameterProperty parameter)
            return;

        var viewModel = FindMainWindowViewModel();
        if (viewModel != null)
        {
            viewModel.StartCropCommand.Execute(parameter);
        }
    }

    private void ClearSampleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ParameterProperty parameter)
        {
            parameter.Value = null;
        }
    }
}

