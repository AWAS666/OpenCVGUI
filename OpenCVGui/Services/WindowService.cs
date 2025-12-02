using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using System.Linq;

namespace OpenCVGui.Services;

/// <summary>
/// Centralized service for accessing application windows.
/// Eliminates repeated IClassicDesktopStyleApplicationLifetime casting throughout the codebase.
/// </summary>
public static class WindowService
{
    private static IClassicDesktopStyleApplicationLifetime? GetDesktopLifetime()
    {
        return Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
    }

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    public static Window? MainWindow => GetDesktopLifetime()?.MainWindow;

    /// <summary>
    /// Gets all open windows in the application.
    /// </summary>
    public static IEnumerable<Window> Windows => GetDesktopLifetime()?.Windows ?? Enumerable.Empty<Window>();

    /// <summary>
    /// Finds the first window of the specified type.
    /// </summary>
    public static T? FindWindow<T>() where T : Window
    {
        return Windows.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Finds all windows of the specified type.
    /// </summary>
    public static IEnumerable<T> FindWindows<T>() where T : Window
    {
        return Windows.OfType<T>();
    }

    /// <summary>
    /// Finds a window of the specified type with the given DataContext.
    /// </summary>
    public static T? FindWindowByDataContext<T>(object dataContext) where T : Window
    {
        return Windows.OfType<T>().FirstOrDefault(w => w.DataContext == dataContext);
    }

    /// <summary>
    /// Checks if the application is running in desktop mode.
    /// </summary>
    public static bool IsDesktopMode => GetDesktopLifetime() != null;
}

