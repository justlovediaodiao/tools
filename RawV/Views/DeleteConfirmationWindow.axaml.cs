using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RawV.Views;

public partial class DeleteConfirmationWindow : Window
{
    public DeleteConfirmationWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
}
