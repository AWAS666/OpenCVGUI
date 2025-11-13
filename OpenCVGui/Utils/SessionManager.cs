using System;
using System.IO;
using System.Text.Json;

namespace OpenCVGui.Utils;

/// <summary>
/// Manages application session data (last loaded file, etc.)
/// </summary>
public static class SessionManager
{
    private static readonly string SessionFilePath;
    
    static SessionManager()
    {
        // Store session file in user's app data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "OpenCVGUI");
        Directory.CreateDirectory(appFolder);
        SessionFilePath = Path.Combine(appFolder, "session.json");
    }
    
    public class SessionData
    {
        public string? LastLoadedFile { get; set; }
    }
    
    /// <summary>
    /// Saves the last loaded file path to session storage
    /// </summary>
    public static void SaveLastLoadedFile(string filePath)
    {
        try
        {
            var sessionData = new SessionData
            {
                LastLoadedFile = filePath
            };
            
            var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets the last loaded file path from session storage
    /// </summary>
    public static string? GetLastLoadedFile()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
            {
                return null;
            }
            
            var json = File.ReadAllText(SessionFilePath);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json);
            
            // Verify the file still exists
            if (sessionData?.LastLoadedFile != null && File.Exists(sessionData.LastLoadedFile))
            {
                return sessionData.LastLoadedFile;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading session: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Clears the session data
    /// </summary>
    public static void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing session: {ex.Message}");
        }
    }
}

