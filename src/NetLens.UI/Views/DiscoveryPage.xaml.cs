using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

/// <summary>
/// Code-behind for DiscoveryPage.xaml. Resolves its ViewModel from DI container.
/// </summary>
public sealed partial class DiscoveryPage : Page
{
    public DiscoveryViewModel ViewModel { get; }

    public DiscoveryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DiscoveryViewModel>();
        DataContext = ViewModel;
    }
}
