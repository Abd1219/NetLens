using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using System.Globalization;

namespace NetLens.UI.Views;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService;
    private readonly LocalizationService _localizationService;

    // Parameterless constructor required by Frame.Navigate(Type)
    public SettingsPage()
    {
        _settingsService = App.Services.GetRequiredService<SettingsService>();
        _localizationService = App.Services.GetRequiredService<LocalizationService>();

        this.InitializeComponent();

        // Populate UI texts
        TitleText.Text = _localizationService.GetString("Settings_Title");
        DescriptionText.Text = _localizationService.GetString("Settings_Description");
        LanguageLabel.Text = _localizationService.GetString("Settings_Language_Label");

        // Populate languages
        LanguageCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Language_English"), Tag = "en-US" });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = _localizationService.GetString("Settings_Language_Spanish"), Tag = "es-ES" });

        var current = _settingsService.GetLanguage();
        // Select current language
        foreach (ComboBoxItem it in LanguageCombo.Items)
        {
            if (it.Tag?.ToString() == current || it.Tag?.ToString() == CultureInfo.CurrentUICulture.Name)
            {
                LanguageCombo.SelectedItem = it;
                break;
            }
        }

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            TitleText.Text = _localizationService.GetString("Settings_Title");
            DescriptionText.Text = _localizationService.GetString("Settings_Description");
            LanguageLabel.Text = _localizationService.GetString("Settings_Language_Label");
            if (LanguageCombo.Items.Count >= 2)
            {
                ((ComboBoxItem)LanguageCombo.Items[0]).Content = _localizationService.GetString("Settings_Language_English");
                ((ComboBoxItem)LanguageCombo.Items[1]).Content = _localizationService.GetString("Settings_Language_Spanish");
            }
        });
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem it && it.Tag is string tag)
        {
            _settingsService.SetLanguage(tag);
            _localizationService.SetLanguage(tag);
        }
    }
}
