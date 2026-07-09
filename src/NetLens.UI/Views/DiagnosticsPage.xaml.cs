using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NetLens.UI.ViewModels;

namespace NetLens.UI.Views;

public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsViewModel ViewModel { get; }

    public DiagnosticsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DiagnosticsViewModel>();
        DataContext = ViewModel;
    }
}
