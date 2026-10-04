using Avalonia.Controls;
using filerename.ViewModels;

namespace filerename.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
