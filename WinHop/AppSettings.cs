using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinHop;

internal sealed class AppSettings
{
    public bool TriggerLeft { get; set; } = false;
    public bool TriggerRight { get; set; } = true;
    public int EdgeTriggerWidth { get; set; } = 20;
    
    public List<string> PinnedProcessNames { get; set; } = new();
    public List<Workspace> Workspaces { get; set; } = new()
    {
        new Workspace { Name = "Work", ProcessNames = new List<string>() },
    };

    private static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WinHop"
            );
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // ignore
        }
    }
}
