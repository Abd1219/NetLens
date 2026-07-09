using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class WifiExplorerPage : Page
{
    public WifiExplorerViewModel ViewModel { get; }

    public WifiExplorerPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<WifiExplorerViewModel>();
        DataContext = ViewModel;
    }
}
