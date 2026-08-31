using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RawV.ViewModels;
using System.Collections.Specialized;

namespace RawV.Views;

public partial class FmtpWindow : Window
{
    private ListBox? _logListBox;

    public FmtpWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private FmtpWindowViewModel ViewModel => (FmtpWindowViewModel)DataContext!;

    private void OnOpened(object? sender, EventArgs e)
    {
        _logListBox = this.FindControl<ListBox>("LogListBox");
        ViewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.LogEntries.Count > 0)
        {
            _logListBox?.ScrollIntoView(ViewModel.LogEntries[^1]);
        }
    }

    private async void OnBrowseLocalDirectoryClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select local sync directory",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.LocalDirectory = path;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnToggleActivityClick(object? sender, RoutedEventArgs e)
        => ViewModel.IsActivityExpanded = !ViewModel.IsActivityExpanded;

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!ViewModel.IsRunning)
        {
            return;
        }

        e.Cancel = true;
        ViewModel.CancelCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        base.OnClosed(e);
    }
}
