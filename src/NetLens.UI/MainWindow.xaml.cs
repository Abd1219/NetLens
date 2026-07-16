using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        { "History", typeof(HistoryPage) }
    };

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        NavigateTo("Dashboard");
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
            // Navigate to Settings page (future v2.0)
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
