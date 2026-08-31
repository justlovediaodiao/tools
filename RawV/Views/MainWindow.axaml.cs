using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RawV.Services;
using RawV.ViewModels;

namespace RawV.Views;

public partial class MainWindow : Window
{
    private ListBox? _thumbnailListBox;
    private ScrollViewer? _scrollViewer;
    private VirtualizingStackPanel? _thumbnailItemsPanel;
    private DispatcherTimer? _debounceTimer;
    private FmtpWindow? _fmtpWindow;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        InitializeDebounceTimer();
        InitializeListBox();
    }

    private void InitializeDebounceTimer()
    {
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
            IsEnabled = false
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            if (DataContext is MainWindowViewModel vm)
            {
                UpdateVisibleRange();
                vm.TriggerLoad();
            }
        };
    }

    private void InitializeListBox()
    {
        // Use UI thread for delayed initialization to ensure ListBox is loaded
        Dispatcher.UIThread.Post(() =>
        {
            _thumbnailListBox = this.FindControl<ListBox>("ThumbnailListBox");
            if (_thumbnailListBox is not null)
            {
                // Find ScrollViewer
                _scrollViewer = _thumbnailListBox.FindDescendantOfType<ScrollViewer>();
                if (_scrollViewer is null)
                {
                    // Subscribe to TemplateApplied event if template is not yet applied
                    _thumbnailListBox.TemplateApplied += OnThumbnailListBoxTemplateApplied;
                }
                else
                {
                    AttachScrollViewerEvents();
                }

                // Listen for Items changes
                _thumbnailListBox.PropertyChanged += (s, e) =>
                {
                    if (e.Property?.Name == "ItemCount")
                    {
                        // Trigger loading when list items change
                        UpdateVisibleRange();
                        RestartDebounceTimer();
                    }
                };
            }
        }, DispatcherPriority.Background);
    }

    private void OnThumbnailListBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_thumbnailListBox is not null)
        {
            _thumbnailListBox.TemplateApplied -= OnThumbnailListBoxTemplateApplied;
            _scrollViewer = _thumbnailListBox.FindDescendantOfType<ScrollViewer>();
            AttachScrollViewerEvents();
        }
    }

    private void AttachScrollViewerEvents()
    {
        if (_scrollViewer is null)
        {
            return;
        }

        _scrollViewer.ScrollChanged += OnScrollChanged;
        _thumbnailItemsPanel = _thumbnailListBox?.FindDescendantOfType<VirtualizingStackPanel>();

        // Initial trigger once
        UpdateVisibleRange();
        RestartDebounceTimer();
    }

    /// <summary>
    /// Handles scroll changed events
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateVisibleRange();
        RestartDebounceTimer();
    }

    /// <summary>
    /// Updates the visible range
    /// </summary>
    private void UpdateVisibleRange()
    {
        if (DataContext is not MainWindowViewModel vm || vm.Thumbnails.Count == 0)
        {
            return;
        }

        _thumbnailItemsPanel ??= _thumbnailListBox?.FindDescendantOfType<VirtualizingStackPanel>();
        if (_thumbnailItemsPanel is null)
        {
            return;
        }

        var firstIndex = _thumbnailItemsPanel.FirstRealizedIndex;
        var lastIndex = _thumbnailItemsPanel.LastRealizedIndex;
        if (firstIndex < 0 || lastIndex < 0)
        {
            return;
        }

        vm.UpdateVisibleRange(
            Math.Clamp(firstIndex, 0, vm.Thumbnails.Count - 1),
            Math.Clamp(lastIndex, 0, vm.Thumbnails.Count - 1));
    }

    /// <summary>
    /// Restarts the debounce timer
    /// </summary>
    private void RestartDebounceTimer()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpenFilesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Image Files",
            AllowMultiple = true
        });

        if (files.Count == 0)
        {
            return;
        }

        await ViewModel.OpenFilesAsync(files.Select(static file => file.TryGetLocalPath()).OfType<string>());
    }

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Image Folder",
            AllowMultiple = false
        });

        var folderPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        await ViewModel.OpenFolderAsync(folderPath);
    }

    private void OnFmtpClick(object? sender, RoutedEventArgs e)
    {
        if (_fmtpWindow is not null)
        {
            _fmtpWindow.Activate();
            return;
        }

        var initialDirectory = ViewModel.CurrentItem is { } item
            ? Path.GetDirectoryName(item.FilePath)
            : null;
        _fmtpWindow = new FmtpWindow
        {
            DataContext = new FmtpWindowViewModel(initialDirectory)
        };
        _fmtpWindow.Closed += (_, _) => _fmtpWindow = null;
        _fmtpWindow.Show(this);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        await DeleteCurrentAsync();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel)
        {
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            e.Handled = true;
            await HandleImageNavigationAsync(e.Key);
            return;
        }

        if (IsDeleteShortcut(e))
        {
            await DeleteCurrentAsync();
            e.Handled = true;
        }
    }

    private static bool IsDeleteShortcut(KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            return true;
        }

        return e.Key == Key.Back && e.KeyModifiers.HasFlag(KeyModifiers.Meta);
    }

    private async Task HandleImageNavigationAsync(Key key)
    {
        if (key is Key.Left or Key.Up)
        {
            if (ViewModel.CanGoPrevious)
            {
                await ViewModel.NavigatePreviousAsync();
            }
        }
        else if (key is Key.Right or Key.Down)
        {
            if (ViewModel.CanGoNext)
            {
                await ViewModel.NavigateNextAsync();
            }
        }
    }

    private async Task DeleteCurrentAsync()
    {
        if (!ViewModel.CanDelete)
        {
            return;
        }

        if (ViewModel.DeleteConfirmationEnabled)
        {
            var dialog = new DeleteConfirmationWindow();
            var deleteButton = this.FindControl<Button>("DeleteButton");
            SuppressToolTip(deleteButton);
            try
            {
                var confirmed = await dialog.ShowDialog<bool>(this);
                if (!confirmed)
                {
                    return;
                }
            }
            finally
            {
                RestoreToolTip(deleteButton);
            }
        }

        await ViewModel.DeleteCurrentAsync();

        // Trigger loading update after deletion
        UpdateVisibleRange();
        RestartDebounceTimer();
    }

    private static void SuppressToolTip(Control? control)
    {
        if (control is null)
        {
            return;
        }

        ToolTip.SetIsOpen(control, false);
        ToolTip.SetServiceEnabled(control, false);
    }

    private static void RestoreToolTip(Control? control)
    {
        if (control is null)
        {
            return;
        }

        ToolTip.SetServiceEnabled(control, true);
    }
}
