using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class HistoryPage : Page
{
    private readonly LocalizationService _loc;

    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
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
        TitleText.Text = _loc.GetString("History_Title");
        SubtitleText.Text = _loc.GetString("History_Subtitle");
        RefreshBtn.Content = _loc.GetString("History_Refresh");
        SessionsHeader.Text = _loc.GetString("History_Sessions");
        Col_DateTime.Text = _loc.GetString("History_DateTime");
        Col_ClientName.Text = _loc.GetString("History_ClientName");
        Col_Site.Text = _loc.GetString("History_Site");
        Col_Snapshots.Text = _loc.GetString("History_Snapshots");
        Col_Events.Text = _loc.GetString("History_Events");
        Col_Action.Text = _loc.GetString("Discovery_Action");
        ExportBtnText.Tag = _loc.GetString("History_ExportPdf");
        NoSessionsText.Text = _loc.GetString("History_NoSessions");
    }
}
