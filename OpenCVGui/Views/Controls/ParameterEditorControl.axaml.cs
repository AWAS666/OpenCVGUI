using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpenCVGui.ViewModels;
using OpenCVGui.Views.Controls.ParameterEditors;

namespace OpenCVGui.Views.Controls;

public partial class ParameterEditorControl : UserControl
{
    public ParameterEditorControl()
    {
        InitializeComponent();
    }
}

public class ParameterEditorTemplateSelector : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is not ParameterProperty parameter)
            return null;

        UserControl editor = parameter.EditorType switch
        {
            "ImageSample" => new ImageSampleParameterEditor(),
            "Slider" => new SliderParameterEditor(),
            "Color" => new ColorParameterEditor(),
            "Enum" => new EnumParameterEditor(),
            "Boolean" => new BooleanParameterEditor(),
            "Numeric" => new NumericParameterEditor(),
            "String" => new StringParameterEditor(),
            _ => new StringParameterEditor()
        };

        editor.DataContext = parameter;
        return editor;
    }

    public bool Match(object? data)
    {
        return data is ParameterProperty;
    }
}

