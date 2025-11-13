using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

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
                    layerData.Parameters.Add(new ParameterData
                    {
                        Name = prop.Name,
                        Value = ConvertToString(value, prop.PropertyType)
                    });
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
                    var value = ConvertFromString(paramData.Value, prop.PropertyType);
                    if (value != null)
                    {
                        prop.SetValue(layer, value);
                    }
                }
            }
        }

        return layer;
    }

    private static string ConvertToString(object value, Type type)
    {
        if (type.IsEnum)
        {
            return value.ToString() ?? string.Empty;
        }

        if (type == typeof(string))
        {
            return (string)value;
        }

        return System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static object? ConvertFromString(string value, Type type)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (type == typeof(string))
        {
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

