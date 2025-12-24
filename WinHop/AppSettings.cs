using System;
using System.IO;
using System.Text.Json;

namespace WinHop;

internal sealed class AppSettings
{
    public bool TriggerLeft { get; set; } = true;
    public bool TriggerRight { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            var path = GetPath();
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
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinHop"
        );
        return Path.Combine(dir, "settings.json");
    }
}


