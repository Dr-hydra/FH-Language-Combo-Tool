using System.Text.Json;
using System.IO;

namespace FH.LanguageComboTool.Wpf;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;

    public AppSettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FHLanguageComboTool");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "wpf-settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}

public sealed record AppSettings
{
    public string? UiLanguage { get; set; }
    public bool DisclaimerAccepted { get; set; }
}
