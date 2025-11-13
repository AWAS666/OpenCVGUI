using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace OpenCVGui.ViewModels;

/// <summary>
/// Caches rendered results per layer to avoid re-rendering when parameters haven't changed
/// </summary>
internal class RenderCache
{
    private readonly Dictionary<Guid, CachedResult> _cache = new();
    private readonly Dictionary<Guid, string> _parameterHashes = new();

    public Mat? GetCachedResult(Guid layerId, string parameterHash)
    {
        if (_cache.TryGetValue(layerId, out var cached) && 
            cached.ParameterHash == parameterHash)
        {
            return cached.Result?.Clone();
        }
        return null;
    }

    public void SetCachedResult(Guid layerId, string parameterHash, Mat result)
    {
        // Dispose old cached result if it exists
        if (_cache.TryGetValue(layerId, out var oldCached))
        {
            oldCached.Result?.Dispose();
        }

        _cache[layerId] = new CachedResult
        {
            ParameterHash = parameterHash,
            Result = result.Clone()
        };
    }

    public void Clear()
    {
        foreach (var cached in _cache.Values)
        {
            cached.Result?.Dispose();
        }
        _cache.Clear();
        _parameterHashes.Clear();
    }

    public void InvalidateLayer(Guid layerId)
    {
        if (_cache.TryGetValue(layerId, out var cached))
        {
            cached.Result?.Dispose();
            _cache.Remove(layerId);
        }
        _parameterHashes.Remove(layerId);
    }

    private class CachedResult
    {
        public string ParameterHash { get; set; } = string.Empty;
        public Mat? Result { get; set; }
    }
}

