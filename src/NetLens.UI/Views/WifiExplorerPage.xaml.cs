using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class WifiExplorerPage : Page
{
    private readonly LocalizationService _loc;

    public WifiExplorerViewModel ViewModel { get; }

    public WifiExplorerPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WifiExplorerViewModel>();
        DataContext = ViewModel;

        _loc = App.Services.GetRequiredService<LocalizationService>();
        ApplyLocalization();

        _loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        _ = DispatcherQueue.TryEnqueue(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        TitleText.Text = _loc.GetString("Wifi_Title");
        SubtitleText.Text = _loc.GetString("Wifi_Subtitle");
        ActiveAssocText.Text = _loc.GetString("Wifi_ActiveAssociation");
        Label_RSSI.Text = _loc.GetString("Card_RSSI");
        Label_Channel.Text = _loc.GetString("Card_Channel");
        SurroundingHeader.Text = _loc.GetString("Wifi_SurroundingAPs");
        Col_Signal.Text = _loc.GetString("Wifi_Signal");
        Col_Channel.Text = _loc.GetString("Card_Channel");
        Col_Security.Text = _loc.GetString("Wifi_Security");
        Col_Standard.Text = _loc.GetString("Wifi_Standard");
    }
}
