using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpenCVGui.ViewModels;
using OpenCVGui.ViewModels.Custom;

namespace OpenCVGui.Views.Controls;

/// <summary>
/// Template selector for custom layer controls
/// </summary>
public class CustomControlTemplateSelector : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is not LayerViewModel layerViewModel)
            return null;

        var controlTypeName = layerViewModel.CustomControlTypeName;
        if (string.IsNullOrEmpty(controlTypeName))
            return null;

        // Map control type names to actual control classes
        UserControl? control = controlTypeName switch
        {
            "PatternLearningControl" => CreatePatternLearningControl(layerViewModel),
            _ => null
        };

        return control;
    }

    private UserControl CreatePatternLearningControl(LayerViewModel layerViewModel)
    {
        var viewModel = new PatternLearningControlViewModel(layerViewModel);
        var control = new CustomControls.PatternLearningControl();
        control.DataContext = viewModel;
        return control;
    }

    public bool Match(object? data)
    {
        return data is LayerViewModel layerViewModel && layerViewModel.HasCustomControl;
    }
}

