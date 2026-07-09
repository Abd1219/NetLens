using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = ViewModel;
    }
}
