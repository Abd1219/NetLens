using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

/// <summary>
/// Code-behind for DiscoveryPage.xaml. Resolves its ViewModel from DI container.
/// </summary>
public sealed partial class DiscoveryPage : Page
{
    private readonly LocalizationService _loc;

    public DiscoveryViewModel ViewModel { get; }

    public DiscoveryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DiscoveryViewModel>();
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
        TitleText.Text = _loc.GetString("Discovery_Title");
        SubtitleText.Text = _loc.GetString("Discovery_Subtitle");
        SubnetScanHeader.Text = _loc.GetString("Discovery_SubnetScan");
        ScanBtn.Content = _loc.GetString("Discovery_ScanNetwork");
        CancelBtn.Content = _loc.GetString("Discovery_Cancel");
        Col_Ip.Text = _loc.GetString("Discovery_IpAddress");
        Col_Mac.Text = _loc.GetString("Discovery_MacAddress");
        Col_Host.Text = _loc.GetString("Discovery_Hostname");
        Col_Latency.Text = _loc.GetString("Discovery_Latency");
        Col_Type.Text = _loc.GetString("Discovery_Type");
        Col_Action.Text = _loc.GetString("Discovery_Action");
        TracerouteTitle.Text = _loc.GetString("Discovery_TracerouteDiagnostics");
    }
}
