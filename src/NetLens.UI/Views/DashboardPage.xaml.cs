using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly LocalizationService _loc;

    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
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
        Label_RSSI.Text = _loc.GetString("Card_RSSI");
        Label_PhyRate.Text = _loc.GetString("Card_PhyRate");
        Label_Gateway.Text = _loc.GetString("Card_Gateway");
        Label_RoundTrip.Text = _loc.GetString("Card_RoundTrip");
        Label_PacketLoss.Text = _loc.GetString("Card_PacketLoss");
        Label_DNS.Text = _loc.GetString("Card_DNS");
        Label_Internet.Text = _loc.GetString("Card_Internet");
        Label_Channel.Text = _loc.GetString("Card_Channel");
        Label_System.Text = _loc.GetString("Card_System");

        Title_ChartRSSI.Text = _loc.GetString("Chart_RSSI");
        Title_ChartGateway.Text = _loc.GetString("Chart_GatewayLatency");
        Title_ChartPacketLoss.Text = _loc.GetString("Chart_PacketLoss");

        Title_ActiveDiagnostics.Text = _loc.GetString("ActiveDiagnostics_Title");
        Label_NoIssues.Text = _loc.GetString("ActiveDiagnostics_NoIssues");

        Label_LocalIp.Text = _loc.GetString("Card_LocalIp");
        Label_GatewayIp.Text = _loc.GetString("Card_GatewayIp");
        Label_DnsServer.Text = _loc.GetString("Card_DnsServer");
    }
}
