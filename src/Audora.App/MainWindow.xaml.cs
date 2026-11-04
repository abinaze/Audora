using System.Windows;
using Audora.App.ViewModels;

namespace Audora.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}
