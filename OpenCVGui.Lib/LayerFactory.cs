using System.Reflection;

namespace OpenCVGui.Lib;

/// <summary>
/// Factory for creating layer instances by type identifier
/// </summary>
public static class LayerFactory
{
    private static readonly Dictionary<string, Type> _layerTypes = new();
    private static readonly Dictionary<string, string> _layerDisplayNames = new();
    private static bool _initialized = false;

    /// <summary>
    /// Initializes the factory by discovering all layer types in the current assembly
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        var assembly = Assembly.GetExecutingAssembly();
        var layerTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BaseLayer)) && !t.IsAbstract);

        foreach (var layerType in layerTypes)
        {
            try
            {
                var instance = (BaseLayer)Activator.CreateInstance(layerType)!;
                var layerTypeId = instance.GetLayerType();
                _layerTypes[layerTypeId] = layerType;
                _layerDisplayNames[layerTypeId] = GetDisplayName(layerType);
            }
            catch
            {
                // Skip types that can't be instantiated
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// Creates a new layer instance of the specified type
    /// </summary>
    public static BaseLayer CreateLayer(string layerType)
    {
        Initialize();

        if (!_layerTypes.TryGetValue(layerType, out var type))
        {
            throw new ArgumentException($"Unknown layer type: {layerType}", nameof(layerType));
        }

        return (BaseLayer)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Gets all available layer types
    /// </summary>
    public static IEnumerable<string> GetAvailableLayerTypes()
    {
        Initialize();
        return _layerTypes.Keys;
    }

    /// <summary>
    /// Gets the display name for a layer type
    /// </summary>
    public static string GetDisplayName(string layerType)
    {
        Initialize();
        return _layerDisplayNames.TryGetValue(layerType, out var name) ? name : layerType;
    }

    /// <summary>
    /// Gets all layer types with their display names
    /// </summary>
    public static Dictionary<string, string> GetAllLayerTypesWithNames()
    {
        Initialize();
        return new Dictionary<string, string>(_layerDisplayNames);
    }

    private static string GetDisplayName(Type layerType)
    {
        // Remove "Layer" suffix if present
        var name = layerType.Name;
        if (name.EndsWith("Layer", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 5);
        }

        // Insert spaces before capital letters
        var result = string.Empty;
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                result += " ";
            }
            result += name[i];
        }

        return result;
    }
}

