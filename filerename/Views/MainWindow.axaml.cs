using SukiUI.Controls;
using filerename.ViewModels;

namespace filerename.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
