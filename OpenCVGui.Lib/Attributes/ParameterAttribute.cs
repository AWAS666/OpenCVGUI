namespace OpenCVGui.Lib.Attributes;

/// <summary>
/// Marks a property as a parameter that should be displayed in the UI
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParameterAttribute : Attribute
{
    /// <summary>
    /// Display name shown in the UI (defaults to property name)
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description/tooltip for this parameter
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping parameters in the UI
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Order in which parameters should be displayed (lower numbers first)
    /// </summary>
    public int Order { get; set; } = int.MaxValue;
}

/// <summary>
/// Specifies range constraints for numeric parameters
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParameterRangeAttribute : Attribute
{
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double Step { get; set; } = 1.0;

    public ParameterRangeAttribute(double minimum, double maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    public ParameterRangeAttribute(double minimum, double maximum, double step)
    {
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }
}

/// <summary>
/// Provides display names for enum values in dropdowns
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParameterEnumAttribute : Attribute
{
    /// <summary>
    /// Dictionary of enum value to display name mappings
    /// </summary>
    public Dictionary<object, string> DisplayNames { get; set; } = new();
}

/// <summary>
/// Marks a parameter as a file path that should use a file picker
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ParameterFileAttribute : Attribute
{
    /// <summary>
    /// File filter for the file dialog (e.g., "Image files|*.png;*.jpg")
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Whether to allow multiple file selection
    /// </summary>
    public bool AllowMultiple { get; set; } = false;
}

