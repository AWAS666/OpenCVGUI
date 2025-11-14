using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Linq;

namespace OpenCVGui.Lib.Serialization;

/// <summary>
/// Handles serialization and deserialization of layer chains to/from XML
/// </summary>
public static class LayerChainSerializer
{
    /// <summary>
    /// Serializes a layer chain to XML format
    /// </summary>
    public static string Serialize(LayerChain chain)
    {
        var data = new LayerChainData
        {
            Layers = chain.Layers.Select(layer => SerializeLayer(layer)).ToList()
        };

        var serializer = new XmlSerializer(typeof(LayerChainData));
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  "
        });

        serializer.Serialize(xmlWriter, data);
        return stringWriter.ToString();
    }

    /// <summary>
    /// Deserializes a layer chain from XML format
    /// </summary>
    public static LayerChain Deserialize(string xml)
    {
        var serializer = new XmlSerializer(typeof(LayerChainData));
        LayerChainData data;

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader);
        data = (LayerChainData)serializer.Deserialize(xmlReader)!;

        var chain = new LayerChain();
        foreach (var layerData in data.Layers)
        {
            var layer = DeserializeLayer(layerData);
            chain.AddLayer(layer);
        }

        return chain;
    }

    private static LayerData SerializeLayer(BaseLayer layer)
    {
        var layerData = new LayerData
        {
            LayerType = layer.GetLayerType(),
            AssemblyQualifiedName = layer.GetType().AssemblyQualifiedName,
            Id = layer.Id,
            Name = layer.Name,
            IsEnabled = layer.IsEnabled
        };

        // Serialize all properties marked with [Parameter] attribute
        var properties = layer.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<Attributes.ParameterAttribute>() != null)
            {
                var value = prop.GetValue(layer);
                if (value != null)
                {
                    // Handle List<string> specially for base64 sample storage
                    if (value is List<string> stringList)
                    {
                        // Serialize list as comma-separated base64 strings
                        var base64Strings = stringList.Where(s => !string.IsNullOrEmpty(s)).ToList();
                        if (base64Strings.Count > 0)
                        {
                            layerData.Parameters.Add(new ParameterData
                            {
                                Name = prop.Name,
                                Value = string.Join("|LIST_SEPARATOR|", base64Strings)
                            });
                        }
                    }
                    else
                    {
                        layerData.Parameters.Add(new ParameterData
                        {
                            Name = prop.Name,
                            Value = ConvertToString(value, prop.PropertyType, prop)
                        });
                    }
                }
            }
        }

        return layerData;
    }

    private static BaseLayer DeserializeLayer(LayerData layerData)
    {
        // Find the layer type
        Type? layerType = null;
        if (!string.IsNullOrEmpty(layerData.AssemblyQualifiedName))
        {
            layerType = Type.GetType(layerData.AssemblyQualifiedName);
        }

        if (layerType == null)
        {
            throw new InvalidOperationException($"Could not find layer type: {layerData.LayerType}");
        }

        // Create instance
        var layer = (BaseLayer)Activator.CreateInstance(layerType)!;
        layer.Id = layerData.Id;
        layer.Name = layerData.Name;
        layer.IsEnabled = layerData.IsEnabled;

        // Restore parameter values
        var properties = layerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<Attributes.ParameterAttribute>() != null)
            {
                var paramData = layerData.Parameters.FirstOrDefault(p => p.Name == prop.Name);
                if (paramData != null)
                {
                    // Handle List<string> specially for base64 sample storage
                    if (prop.PropertyType == typeof(List<string>))
                    {
                        var list = new List<string>();
                        if (!string.IsNullOrEmpty(paramData.Value))
                        {
                            var items = paramData.Value.Split(new[] { "|LIST_SEPARATOR|" }, StringSplitOptions.None);
                            list.AddRange(items.Where(s => !string.IsNullOrEmpty(s)));
                        }
                        prop.SetValue(layer, list);
                    }
                    else
                    {
                        var value = ConvertFromString(paramData.Value, prop.PropertyType, prop);
                        if (value != null)
                        {
                            prop.SetValue(layer, value);
                        }
                    }
                }
            }
        }

        return layer;
    }

    private static string ConvertToString(object value, Type type, PropertyInfo? property = null)
    {
        if (type.IsEnum)
        {
            return value.ToString() ?? string.Empty;
        }

        if (type == typeof(string))
        {
            var stringValue = (string)value;
            
            // Check if this is a file parameter and if it's an image file
            if (property != null && !string.IsNullOrEmpty(stringValue) && File.Exists(stringValue))
            {
                var fileAttr = property.GetCustomAttribute<Attributes.ParameterFileAttribute>();
                if (fileAttr != null && IsImageFile(fileAttr.Filter))
                {
                    // Convert image file to base64
                    try
                    {
                        var imageBytes = File.ReadAllBytes(stringValue);
                        var base64 = Convert.ToBase64String(imageBytes);
                        return $"base64:{base64}";
                    }
                    catch
                    {
                        // If reading fails, fall back to file path
                        return stringValue;
                    }
                }
            }
            
            return stringValue;
        }

        return System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool IsImageFile(string? filter)
    {
        if (string.IsNullOrEmpty(filter))
            return false;

        // Check if filter contains image file extensions
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif", ".webp" };
        var lowerFilter = filter.ToLowerInvariant();
        return imageExtensions.Any(ext => lowerFilter.Contains(ext));
    }

    private static object? ConvertFromString(string value, Type type, PropertyInfo? property = null)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (type == typeof(string))
        {
            // Check if this is a base64-encoded image
            if (value.StartsWith("base64:", StringComparison.Ordinal))
            {
                try
                {
                    var base64Data = value.Substring(7); // Remove "base64:" prefix
                    var imageBytes = Convert.FromBase64String(base64Data);
                    
                    // Determine file extension based on property's file filter or default to .png
                    var extension = ".png";
                    if (property != null)
                    {
                        var fileAttr = property.GetCustomAttribute<Attributes.ParameterFileAttribute>();
                        if (fileAttr != null && !string.IsNullOrEmpty(fileAttr.Filter))
                        {
                            // Try to extract extension from filter
                            var match = System.Text.RegularExpressions.Regex.Match(fileAttr.Filter, @"\*\.(\w+)");
                            if (match.Success)
                            {
                                extension = "." + match.Groups[1].Value;
                            }
                        }
                    }
                    
                    // Save to temp file
                    var tempPath = Path.Combine(Path.GetTempPath(), $"serialized_image_{Guid.NewGuid()}{extension}");
                    File.WriteAllBytes(tempPath, imageBytes);
                    return tempPath;
                }
                catch
                {
                    // If base64 decode fails, return as-is (might be corrupted data)
                    return value;
                }
            }
            
            return value;
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, value);
        }

        if (type == typeof(int))
        {
            return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type == typeof(double))
        {
            return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type == typeof(float))
        {
            return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (type == typeof(bool))
        {
            return bool.Parse(value);
        }

        return System.Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
    }

    [XmlRoot("LayerChain")]
    public class LayerChainData
    {
        [XmlArray("Layers")]
        [XmlArrayItem("Layer")]
        public List<LayerData> Layers { get; set; } = new();
    }

    public class LayerData
    {
        [XmlAttribute("Type")]
        public string LayerType { get; set; } = string.Empty;

        [XmlAttribute("AssemblyQualifiedName")]
        public string? AssemblyQualifiedName { get; set; }

        [XmlAttribute("Id")]
        public Guid Id { get; set; }

        [XmlAttribute("Name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("IsEnabled")]
        public bool IsEnabled { get; set; } = true;

        [XmlElement("Parameter")]
        public List<ParameterData> Parameters { get; set; } = new();
    }

    public class ParameterData
    {
        [XmlAttribute("Name")]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute("Value")]
        public string Value { get; set; } = string.Empty;
    }
}

