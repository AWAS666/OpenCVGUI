using Avalonia.Controls;
using System;
using System.IO;
using System.Text.Json;

namespace OpenCVGui.Utils;

public class ScreenPosition
{
	public double Width { get; set; }
	public double Height { get; set; }
	public int Left { get; set; }
	public int Top { get; set; }
	public WindowState WindowState { get; set; }

	private static string GetAppDataFolder()
	{
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		var appFolder = Path.Combine(appDataPath, "OpenCVGUI");
		Directory.CreateDirectory(appFolder);
		return appFolder;
	}

	public static void Save(ScreenPosition pos)
	{
		try
		{
			var appFolder = GetAppDataFolder();
			var path = Path.Combine(appFolder, "screen.json");
			File.WriteAllText(path, JsonSerializer.Serialize(pos));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error saving screen position: {ex.Message}");
		}
	}

	public static ScreenPosition? Load()
	{
		try
		{
			var appFolder = GetAppDataFolder();
			var path = Path.Combine(appFolder, "screen.json");
			if (!File.Exists(path))
				return null;
			return JsonSerializer.Deserialize<ScreenPosition>(File.ReadAllText(path));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading screen position: {ex.Message}");
			return null;
		}
	}
}