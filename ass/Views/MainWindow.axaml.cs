using Avalonia.Controls;
using Ass.ViewModels;

namespace Ass.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
