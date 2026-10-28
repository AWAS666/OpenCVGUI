# OpenCV GUI

Desktop app for chaining OpenCV image operations. Layers run in order; each one takes the previous layer's output. Chains save and load as XML.

Windows only. The library uses the OpenCvSharp Windows runtime.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run

```
dotnet run --project OpenCVGui
```

```
dotnet test
```

## Layout

- `OpenCVGui` — Avalonia UI (CommunityToolkit.Mvvm)
- `OpenCVGui.Lib` — layers, chain, and XML serialization
- `OpenCVGui.LibTests` — library tests

## Adding a layer

Subclass `BaseLayer` under `OpenCVGui.Lib`, implement `GetLayerType` and `RenderInternal`, and mark settings with `[Parameter]`. `LayerFactory` discovers concrete subclasses.

Folders: `Import`, `Transform`, `Contour`, `Detection`, `Draw`.
