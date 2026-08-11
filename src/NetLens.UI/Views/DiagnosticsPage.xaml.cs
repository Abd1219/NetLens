using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.Services;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class DiagnosticsPage : Page
{
    private readonly LocalizationService _loc;

    public DiagnosticsViewModel ViewModel { get; }

    public DiagnosticsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DiagnosticsViewModel>();
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
        TitleText.Text = _loc.GetString("Diagnostics_Title");
        SubtitleText.Text = _loc.GetString("Diagnostics_Subtitle");
        ManualScanText.Text = _loc.GetString("Diagnostics_ManualScan");
        RunScanBtn.Content = _loc.GetString("Diagnostics_RunScan");
        HealthScoreLabel.Text = _loc.GetString("Diagnostics_HealthScore");
        ActiveViolationsHeader.Text = _loc.GetString("Diagnostics_ActiveViolations");
        NoViolationsText.Text = _loc.GetString("Diagnostics_NoViolations");
    }
}
