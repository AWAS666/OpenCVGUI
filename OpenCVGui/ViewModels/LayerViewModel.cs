using CommunityToolkit.Mvvm.ComponentModel;
using OpenCVGui.Lib;
using OpenCVGui.Lib.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace OpenCVGui.ViewModels;

/// <summary>
/// ViewModel wrapper for a BaseLayer instance
/// </summary>
public partial class LayerViewModel : ViewModelBase
{
    private readonly BaseLayer _layer;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPinned;

    public BaseLayer Layer => _layer;

    public string Name
    {
        get => _layer.Name;
        set
        {
            if (_layer.Name != value)
            {
                _layer.Name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string LayerType => _layer.GetLayerType();

    public string DisplayName => string.IsNullOrEmpty(Name) ? LayerType : Name;

    public bool IsEnabled
    {
        get => _layer.IsEnabled;
        set
        {
            if (_layer.IsEnabled != value)
            {
                _layer.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public double LastRuntime => Math.Round(_layer.LastRuntime, 2);

    public bool HasError => !string.IsNullOrEmpty(_layer.LastError);

    public ObservableCollection<ParameterProperty> Parameters { get; } = new();

    /// <summary>
    /// Filtered parameters for display when custom control exists (excludes sample paths and model path)
    /// </summary>
    public ObservableCollection<ParameterProperty> FilteredParameters { get; } = new();

    private void UpdateFilteredParameters()
    {
        FilteredParameters.Clear();
        
        if (!HasCustomControl)
        {
            foreach (var param in Parameters)
            {
                FilteredParameters.Add(param);
            }
        }
        else
        {
            // When custom control exists, filter out sample paths, base64 lists, and model path
            // These are managed by the custom control
            var excludedNames = new[] { "PositiveSamplesPath", "NegativeSamplesPath", "TestSamplesPath", "ModelPath", 
                "PositiveSamplesBase64", "NegativeSamplesBase64", "TestSamplesBase64" };
            foreach (var param in Parameters.Where(p => !excludedNames.Contains(p.PropertyName)))
            {
                FilteredParameters.Add(param);
            }
        }
    }

    /// <summary>
    /// Whether this layer has a custom control
    /// </summary>
    public bool HasCustomControl
    {
        get
        {
            var attr = _layer.GetType().GetCustomAttribute<CustomControlAttribute>();
            return attr != null;
        }
    }

    /// <summary>
    /// Name of the custom control type for this layer
    /// </summary>
    public string? CustomControlTypeName
    {
        get
        {
            var attr = _layer.GetType().GetCustomAttribute<CustomControlAttribute>();
            return attr?.ControlTypeName;
        }
    }

    /// <summary>
    /// Whether to show standard parameter editor (inverse of HasCustomControl)
    /// </summary>
    public bool ShowStandardEditor => !HasCustomControl;

    public LayerViewModel(BaseLayer layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));

        // Initialize default name if empty
        if (string.IsNullOrEmpty(_layer.Name))
        {
            _layer.Name = LayerType;
        }

        // Discover and create parameter properties
        DiscoverParameters();
    }

    private void DiscoverParameters()
    {
        var properties = _layer.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var parameterProperties = properties
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null)
            .Select(p => new ParameterProperty(p, _layer))
            .OrderBy(p => p.Order)
            .ThenBy(p => p.DisplayName)
            .ToList();

        foreach (var param in parameterProperties)
        {
            Parameters.Add(param);

            // Subscribe to value changes to trigger re-render
            param.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ParameterProperty.Value))
                {
                    OnPropertyChanged(nameof(Parameters));
                }
            };
        }

        // Update filtered parameters after discovering all parameters
        UpdateFilteredParameters();
        
        // Subscribe to Parameters collection changes to update filtered parameters
        Parameters.CollectionChanged += (s, e) =>
        {
            UpdateFilteredParameters();
        };
    }

    public void RefreshRuntime()
    {
        OnPropertyChanged(nameof(LastRuntime));
        OnPropertyChanged(nameof(HasError));
    }
}

