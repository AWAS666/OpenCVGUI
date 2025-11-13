using CommunityToolkit.Mvvm.ComponentModel;
using OpenCVGui.Lib.Attributes;
using System;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using OpenCVGui.Utils;

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
        
        // Ensure the initial value is properly read and notified
        // This helps UI controls get the correct default value
        OnPropertyChanged(nameof(Value));
        if (IsColor)
        {
            OnPropertyChanged(nameof(ColorValue));
        }
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
            
            // If this is a color property, notify ColorValue changed too
            if (IsColor)
            {
                OnPropertyChanged(nameof(ColorValue));
            }
            
            // If this is an image sample property, notify HasSample, SampleDimensions, and SamplePreviewBitmap changed
            if (IsImageSample)
            {
                OnPropertyChanged(nameof(HasSample));
                OnPropertyChanged(nameof(SampleDimensions));
                OnPropertyChanged(nameof(SamplePreviewBitmap));
            }
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

    public ParameterImageSampleAttribute? GetImageSampleAttribute()
    {
        return _propertyInfo.GetCustomAttribute<ParameterImageSampleAttribute>();
    }

    public bool IsImageSample => GetImageSampleAttribute() != null;

    public bool IsColor => _propertyInfo.GetCustomAttribute<ParameterColorAttribute>() != null;

    public bool IsEnum => PropertyType.IsEnum;

    public bool IsBoolean => PropertyType == typeof(bool);

    public bool IsNumeric => PropertyType == typeof(int) || 
                              PropertyType == typeof(double) || 
                              PropertyType == typeof(float) ||
                              PropertyType == typeof(decimal);

    public bool IsString => PropertyType == typeof(string);

    public bool IsStringNotColor => IsString && !IsColor;

    public string EditorType
    {
        get
        {
            if (IsImageSample)
                return "ImageSample";
            if (RangeAttribute != null && IsNumeric)
                return "Slider";
            if (IsColor)
                return "Color";
            if (IsEnum)
                return "Enum";
            if (IsBoolean)
                return "Boolean";
            if (IsNumeric)
                return "Numeric";
            if (IsStringNotColor)
                return "String";
            return "String";
        }
    }

    public Array GetEnumValues()
    {
        if (!IsEnum)
            return Array.Empty<object>();

        return Enum.GetValues(PropertyType);
    }

    public Array EnumValues => GetEnumValues();

    /// <summary>
    /// Color value for color picker binding (converts between hex string and Color)
    /// </summary>
    public Color ColorValue
    {
        get
        {
            if (!IsColor || Value is not string hexValue || string.IsNullOrWhiteSpace(hexValue))
                return Colors.White;

            return HexToColor(hexValue);
        }
        set
        {
            if (IsColor)
            {
                var hex = ColorToHex(value);
                var currentHex = Value as string;
                
                // Only update if different (avoid circular updates)
                if (currentHex != hex)
                {
                    _propertyInfo.SetValue(_layerInstance, hex);
                    OnPropertyChanged(nameof(Value));
                    // Don't call OnPropertyChanged for ColorValue here to avoid recursion
                }
            }
        }
    }

    private static Color HexToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Colors.White;
        }

        hex = hex.TrimStart('#').Trim();
        
        // Handle partial input - pad with zeros
        if (hex.Length < 6)
        {
            hex = hex.PadRight(6, '0');
        }
        else if (hex.Length > 6)
        {
            hex = hex.Substring(0, 6);
        }

        try
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return Colors.White;
        }
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public bool HasSample
    {
        get
        {
            if (!IsImageSample)
                return false;

            var value = Value;
            if (value == null)
                return false;

            if (value is Mat mat)
                return !mat.Empty();
            
            if (value is byte[] bytes)
                return bytes.Length > 0;
            
            if (value is string str)
                return !string.IsNullOrWhiteSpace(str);

            return false;
        }
    }

    public string SampleDimensions
    {
        get
        {
            if (!HasSample)
                return string.Empty;

            try
            {
                var value = Value;
                if (value is Mat mat && !mat.Empty())
                {
                    return $"{mat.Width} × {mat.Height}";
                }
                else if (value is byte[] bytes && bytes.Length > 0)
                {
                    // Try to decode to get dimensions
                    var tempMat = Cv2.ImDecode(bytes, ImreadModes.Color);
                    if (!tempMat.Empty())
                    {
                        var dims = $"{tempMat.Width} × {tempMat.Height}";
                        tempMat.Dispose();
                        return dims;
                    }
                }
                else if (value is string path && !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                {
                    var mat1 = Cv2.ImRead(path, ImreadModes.Color);
                    if (!mat1.Empty())
                    {
                        var dims = $"{mat1.Width} × {mat1.Height}";
                        mat1.Dispose();
                        return dims;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return "Unknown";
        }
    }

    public Bitmap? SamplePreviewBitmap
    {
        get
        {
            if (!HasSample)
                return null;

            try
            {
                var value = Value;
                Mat? mat = null;
                bool shouldDisposeMat = false;

                if (value is Mat m && !m.Empty())
                {
                    // Don't clone - use directly, we don't own it
                    mat = m;
                    shouldDisposeMat = false;
                }
                else if (value is byte[] bytes && bytes.Length > 0)
                {
                    mat = Cv2.ImDecode(bytes, ImreadModes.Color);
                    shouldDisposeMat = true;
                }
                else if (value is string path && !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                {
                    mat = Cv2.ImRead(path, ImreadModes.Color);
                    shouldDisposeMat = true;
                }

                if (mat != null && !mat.Empty())
                {
                    var bitmap = ImageConverter.MatToBitmapSafe(mat);
                    if (shouldDisposeMat)
                    {
                        mat.Dispose();
                    }
                    return bitmap;
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }
    }
}

