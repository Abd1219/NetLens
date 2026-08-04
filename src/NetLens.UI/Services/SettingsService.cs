using System.Globalization;
using System.Text.Json;

namespace NetLens.UI.Services;

public class AppSettings
{
    public string? Language { get; set; }
}

public class SettingsService
{
    private const string SettingsFileName = "netlens.settings.json";
    private readonly string _path;
    private AppSettings _settings = new();

    public SettingsService()
    {
        _path = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // ignore write failures for now
        }
    }

    public string GetLanguage()
    {
        return _settings.Language ?? CultureInfo.CurrentUICulture.Name;
    }

    public void SetLanguage(string cultureName)
    {
        _settings.Language = cultureName;
        Save();
    }
}
