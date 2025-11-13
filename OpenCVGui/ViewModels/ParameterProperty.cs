using CommunityToolkit.Mvvm.ComponentModel;
using OpenCVGui.Lib.Attributes;
using System;
using System.Reflection;

namespace OpenCVGui.ViewModels;

/// <summary>
/// Wraps a property from a layer with metadata for UI binding
/// </summary>
public partial class ParameterProperty : ObservableObject
{
    private readonly PropertyInfo _propertyInfo;
    private readonly object _layerInstance;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private int _order;

    public string PropertyName => _propertyInfo.Name;
    public Type PropertyType => _propertyInfo.PropertyType;

    public ParameterProperty(PropertyInfo propertyInfo, object layerInstance)
    {
        _propertyInfo = propertyInfo ?? throw new ArgumentNullException(nameof(propertyInfo));
        _layerInstance = layerInstance ?? throw new ArgumentNullException(nameof(layerInstance));

        var paramAttr = propertyInfo.GetCustomAttribute<ParameterAttribute>();
        DisplayName = paramAttr?.DisplayName ?? propertyInfo.Name;
        Description = paramAttr?.Description;
        Category = paramAttr?.Category;
        Order = paramAttr?.Order ?? int.MaxValue;
    }

    public object? Value
    {
        get => _propertyInfo.GetValue(_layerInstance);
        set
        {
            if (value != null && !PropertyType.IsAssignableFrom(value.GetType()))
            {
                // Try to convert the value
                try
                {
                    value = Convert.ChangeType(value, PropertyType);
                }
                catch
                {
                    return; // Conversion failed
                }
            }

            _propertyInfo.SetValue(_layerInstance, value);
            OnPropertyChanged(nameof(Value));
        }
    }

    public T? GetValue<T>()
    {
        var value = Value;
        if (value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public void SetValue<T>(T value)
    {
        Value = value;
    }

    public ParameterRangeAttribute? GetRangeAttribute()
    {
        return _propertyInfo.GetCustomAttribute<ParameterRangeAttribute>();
    }

    public ParameterRangeAttribute? RangeAttribute => GetRangeAttribute();

    public ParameterFileAttribute? GetFileAttribute()
    {
        return _propertyInfo.GetCustomAttribute<ParameterFileAttribute>();
    }

    public bool IsEnum => PropertyType.IsEnum;

    public bool IsBoolean => PropertyType == typeof(bool);

    public bool IsNumeric => PropertyType == typeof(int) || 
                              PropertyType == typeof(double) || 
                              PropertyType == typeof(float) ||
                              PropertyType == typeof(decimal);

    public bool IsString => PropertyType == typeof(string);

    public Array GetEnumValues()
    {
        if (!IsEnum)
            return Array.Empty<object>();

        return Enum.GetValues(PropertyType);
    }

    public Array EnumValues => GetEnumValues();
}

