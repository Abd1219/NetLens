using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using NetLens.UI.Views;
using Windows.Graphics;

namespace NetLens.UI;

/// <summary>
/// Main application window. Contains the NavigationView shell.
/// Only initialization logic lives here — no business logic.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Type> _pageMap = new()
    {
        { "Dashboard", typeof(DashboardPage) },
        { "WifiExplorer", typeof(WifiExplorerPage) },
        { "Discovery", typeof(DiscoveryPage) },
        { "Diagnostics", typeof(DiagnosticsPage) },
        { "History", typeof(HistoryPage) },
        { "Settings", typeof(SettingsPage) }
    };

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        ApplyLocalization();
        NavigateTo("Dashboard");
    }

    private void ApplyLocalization()
    {
        try
        {
            var loc = App.Services.GetRequiredService<Services.LocalizationService>();
            // Set window title
            this.Title = loc.GetString("Title");

            // Pane header
            PaneHeaderTitle.Text = loc.GetString("PaneHeader_Title");
            PaneHeaderSubtitle.Text = loc.GetString("PaneHeader_Subtitle");

            // Menu items
            NavItem_Dashboard.Content = loc.GetString("Menu_Dashboard");
            ToolTipService.SetToolTip(NavItem_Dashboard, loc.GetString("ToolTip_Dashboard"));

            NavItem_Wifi.Content = loc.GetString("Menu_Wifi");

            NavItem_Discovery.Content = loc.GetString("Menu_Discovery");
            ToolTipService.SetToolTip(NavItem_Discovery, loc.GetString("ToolTip_Discovery"));

            NavItem_Diagnostics.Content = loc.GetString("Menu_Diagnostics");
            ToolTipService.SetToolTip(NavItem_Diagnostics, loc.GetString("ToolTip_Diagnostics"));

            NavItem_History.Content = loc.GetString("Menu_History");
            ToolTipService.SetToolTip(NavItem_History, loc.GetString("ToolTip_History"));

            StatusText.Text = loc.GetString("Status_Monitoring");

            // Subscribe to future language changes
            loc.LanguageChanged -= ApplyLocalization;
            loc.LanguageChanged += ApplyLocalization;
        }
        catch
        {
            // ignore localization errors
        }
    }

    private void ConfigureWindow()
    {
        Title = "NetLens — Network Diagnostic Platform";

        // Set minimum window size and initial size
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new SizeInt32(1440, 900));
        appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo("Settings");
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        if (_pageMap.TryGetValue(tag, out var pageType))
        {
            ContentFrame.Navigate(pageType);
        }

        // Update status dot (will be data-bound in future iteration)
        NavView.SelectedItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == tag);
    }
}
