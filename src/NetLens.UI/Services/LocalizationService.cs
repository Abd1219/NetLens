using System.Globalization;
using System.Resources;

namespace NetLens.UI.Services;

public class LocalizationService
{
    private readonly ResourceManager _rm;

    public event Action? LanguageChanged;

    public LocalizationService()
    {
        // Base name corresponds to folder + filename: NetLens.UI.Resources.Resources
        _rm = new ResourceManager("NetLens.UI.Resources.Resources", typeof(LocalizationService).Assembly);
    }

    public string GetString(string key)
    {
        try
        {
            return _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    public void SetLanguage(string cultureName)
    {
        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            // Also set current thread for immediate effect
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            LanguageChanged?.Invoke();
        }
        catch
        {
            // ignore invalid culture
        }
    }
}
