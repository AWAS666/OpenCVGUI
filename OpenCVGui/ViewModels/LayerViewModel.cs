using CommunityToolkit.Mvvm.ComponentModel;
using OpenCVGui.Lib;
using OpenCVGui.Lib.Attributes;
using System;
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

    public ObservableCollection<ParameterProperty> Parameters { get; } = new();

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
    }

    public void RefreshRuntime()
    {
        OnPropertyChanged(nameof(LastRuntime));
    }
}

