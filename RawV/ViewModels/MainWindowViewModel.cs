using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawV.Models;
using RawV.Services;
using System.Collections.ObjectModel;

namespace RawV.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ImageCatalogService _imageCatalogService = new();
    private readonly ImageLoaderService _imageLoaderService = new();
    private readonly FileDeletionService _fileDeletionService = new();
    private readonly ThumbnailService _thumbnailService = new();

    [ObservableProperty]
    private Bitmap? currentBitmap;

    [ObservableProperty]
    private BrowserSession currentSession = new(Array.Empty<ImageEntry>(), -1);

    [ObservableProperty]
    private string currentFileName = string.Empty;

    [ObservableProperty]
    private string currentStatus = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool deleteConfirmationEnabled = true;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isDeleting;

    [ObservableProperty]
    private ObservableCollection<ThumbnailItemViewModel> thumbnails = new();

    [ObservableProperty]
    private ThumbnailItemViewModel? selectedThumbnail;

    [ObservableProperty]
    private bool isSidebarVisible = false;

    // Serial loading related
    private readonly Queue<int> _loadQueue = new();
    private int _visibleStartIndex = -1;
    private int _visibleEndIndex = -1;
    private int _navigationVersion;
    private bool _isProcessingLoadQueue;

    public bool HasImage => CurrentItem is not null && CurrentBitmap is not null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmptyState => CurrentSession.Items.Count == 0 && !IsBusy;

    public bool CanGoPrevious => CurrentSession.CurrentIndex > 0;

    public bool CanGoNext => CurrentSession.CurrentIndex >= 0 && CurrentSession.CurrentIndex < CurrentSession.Items.Count - 1;

    public bool CanDelete => CurrentItem is not null && !IsDeleting;

    public ImageEntry? CurrentItem => CurrentSession.CurrentIndex >= 0 && CurrentSession.CurrentIndex < CurrentSession.Items.Count
        ? CurrentSession.Items[CurrentSession.CurrentIndex]
        : null;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private Task PreviousAsync() => NavigateToAsync(CurrentSession.CurrentIndex - 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private Task NextAsync() => NavigateToAsync(CurrentSession.CurrentIndex + 1);

    public async Task OpenFilesAsync(IEnumerable<string> filePaths)
    {
        await LoadSessionAsync(_imageCatalogService.CreateFromFiles(filePaths));
    }

    public async Task OpenFolderAsync(string folderPath)
    {
        await LoadSessionAsync(_imageCatalogService.CreateFromDirectory(folderPath));
    }

    public async Task<bool> DeleteCurrentAsync()
    {
        if (CurrentItem is null || IsDeleting)
        {
            return false;
        }

        var itemToDelete = CurrentItem;
        IsDeleting = true;
        IsBusy = true;
        ErrorMessage = string.Empty;

        var result = await _fileDeletionService.DeleteToRecycleBinAsync(itemToDelete, CurrentSession.Items);
        if (!result.Success)
        {
            if (result.CurrentImageDeleted)
            {
                await RemoveDeletedItemAndNavigateAsync(itemToDelete, null, BuildPartialDeleteWarning(result.ErrorMessage));
                IsDeleting = false;
                NotifyStateChanged();
                return false;
            }

            IsBusy = false;
            IsDeleting = false;
            ErrorMessage = BuildDeleteFailureMessage(result);
            NotifyStateChanged();
            return false;
        }

        await RemoveDeletedItemAndNavigateAsync(itemToDelete, result.NextIndex, null);
        IsDeleting = false;
        NotifyStateChanged();
        return true;
    }

    public Task NavigatePreviousAsync() => NavigateToAsync(CurrentSession.CurrentIndex - 1);

    public Task NavigateNextAsync() => NavigateToAsync(CurrentSession.CurrentIndex + 1);

    private async Task LoadSessionAsync(BrowserSession session)
    {
        CurrentBitmap?.Dispose();
        CurrentBitmap = null;
        _navigationVersion++;
        IsBusy = false;

        Thumbnails.Clear();
        _thumbnailService.ClearCache();

        _loadQueue.Clear();

        CurrentSession = session;
        ErrorMessage = string.Empty;

        if (!session.HasItems)
        {
            CurrentFileName = "No images found";
            CurrentStatus = string.Empty;
            SelectedThumbnail = null;
            NotifyStateChanged();
            return;
        }

        InitializeThumbnails();

        // Automatically show sidebar after opening photos
        IsSidebarVisible = true;

        await NavigateToAsync(session.CurrentIndex);
    }

    private void InitializeThumbnails()
    {
        var thumbnailList = new List<ThumbnailItemViewModel>();
        for (int i = 0; i < CurrentSession.Items.Count; i++)
        {
            var item = CurrentSession.Items[i];
            thumbnailList.Add(new ThumbnailItemViewModel
            {
                FilePath = item.FilePath,
                FileName = item.FileName,
                Index = i,
                IsSelected = i == CurrentSession.CurrentIndex
            });
        }

        foreach (var thumbnail in thumbnailList)
        {
            Thumbnails.Add(thumbnail);
        }
    }

    /// <summary>
    /// Updates the realized index range of the virtualization panel.
    /// </summary>
    public void UpdateVisibleRange(int startIndex, int endIndex)
    {
        _visibleStartIndex = startIndex;
        _visibleEndIndex = endIndex;
    }

    /// <summary>
    /// Triggers loading (called after 200ms debounce)
    /// </summary>
    public void TriggerLoad()
    {
        if (_visibleStartIndex < 0 || _visibleEndIndex < 0 || Thumbnails.Count == 0)
        {
            return;
        }

        // Calculate extended 50% range loading area
        var visibleCount = _visibleEndIndex - _visibleStartIndex + 1;
        var bufferCount = Math.Max(1, (int)(visibleCount * 0.5));

        var loadStart = Math.Max(0, _visibleStartIndex - bufferCount);
        var loadEnd = Math.Min(Thumbnails.Count - 1, _visibleEndIndex + bufferCount);

        // Add indexes that need loading to the queue (exclude already loaded and loading)
        for (int i = loadStart; i <= loadEnd; i++)
        {
            if (!Thumbnails[i].IsLoaded && !Thumbnails[i].IsLoading && !_loadQueue.Contains(i))
            {
                _loadQueue.Enqueue(i);
            }
        }

        // Start serial loading
        _ = ProcessLoadQueueAsync();
    }

    /// <summary>
    /// Processes the loading queue serially
    /// </summary>
    private async Task ProcessLoadQueueAsync()
    {
        if (_isProcessingLoadQueue)
        {
            return;
        }

        _isProcessingLoadQueue = true;
        try
        {
            while (_loadQueue.Count > 0)
            {
                var index = _loadQueue.Dequeue();

                // Check if index is still valid
                if (index < 0 || index >= Thumbnails.Count)
                {
                    continue;
                }

                var thumbnail = Thumbnails[index];

                // Skip already loaded or loading
                if (thumbnail.IsLoaded || thumbnail.IsLoading)
                {
                    continue;
                }

                // Serially load thumbnail
                await thumbnail.LoadAsync(_thumbnailService, 120, 90);
            }
        }
        finally
        {
            _isProcessingLoadQueue = false;
        }
    }

    private async Task NavigateToAsync(int index)
    {
        if (index < 0 || index >= CurrentSession.Items.Count)
        {
            NotifyStateChanged();
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        var navigationVersion = ++_navigationVersion;

        var item = CurrentSession.Items[index];
        CurrentBitmap?.Dispose();
        CurrentBitmap = null;

        Bitmap? loadedBitmap = null;
        try
        {
            loadedBitmap = await _imageLoaderService.LoadAsync(item.FilePath);
        }
        catch (Exception ex)
        {
            if (navigationVersion == _navigationVersion)
            {
                ErrorMessage = ex.Message;
            }
        }

        if (navigationVersion != _navigationVersion)
        {
            loadedBitmap?.Dispose();
            return;
        }

        CurrentBitmap = loadedBitmap;
        CurrentSession = new BrowserSession(CurrentSession.Items, index);
        CurrentFileName = item.FileName;
        CurrentStatus = BuildStatus(item, CurrentBitmap, index, CurrentSession.Items.Count);
        IsBusy = false;

        UpdateThumbnailSelection();
        NotifyStateChanged();
    }

    private void UpdateThumbnailSelection()
    {
        foreach (var thumb in Thumbnails)
        {
            thumb.IsSelected = thumb.Index == CurrentSession.CurrentIndex;
        }

        SelectedThumbnail = Thumbnails.FirstOrDefault(t => t.Index == CurrentSession.CurrentIndex);
    }

    private async Task RemoveDeletedItemAndNavigateAsync(ImageEntry deletedItem, int? nextIndex, string? warningMessage)
    {
        var remainingItems = CurrentSession.Items
            .Where(item => !string.Equals(item.FilePath, deletedItem.FilePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _thumbnailService.InvalidateCache(deletedItem.FilePath);

        var thumbnailToRemove = Thumbnails.FirstOrDefault(t => t.FilePath == deletedItem.FilePath);
        if (thumbnailToRemove is not null)
        {
            Thumbnails.Remove(thumbnailToRemove);
        }

        for (int i = 0; i < Thumbnails.Count; i++)
        {
            var thumb = Thumbnails[i];
            Thumbnails[i] = new ThumbnailItemViewModel
            {
                FilePath = thumb.FilePath,
                FileName = thumb.FileName,
                Index = i,
                Thumbnail = thumb.Thumbnail,
                IsLoaded = thumb.IsLoaded
            };
        }

        CurrentBitmap?.Dispose();
        CurrentBitmap = null;
        CurrentSession = new BrowserSession(remainingItems, ResolveNextIndexAfterDeletion(deletedItem, nextIndex, remainingItems.Length) ?? -1);
        ErrorMessage = string.Empty;
        IsBusy = false;

        if (CurrentSession.HasItems)
        {
            await NavigateToAsync(CurrentSession.CurrentIndex);
            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                CurrentStatus = $"{CurrentStatus} · {warningMessage}";
                NotifyStateChanged();
            }
            return;
        }

        SelectedThumbnail = null;
        CurrentFileName = "All images deleted";
        CurrentStatus = warningMessage ?? string.Empty;
        NotifyStateChanged();
    }

    private int? ResolveNextIndexAfterDeletion(ImageEntry deletedItem, int? requestedNextIndex, int remainingCount)
    {
        if (remainingCount == 0)
        {
            return null;
        }

        if (requestedNextIndex.HasValue)
        {
            return Math.Clamp(requestedNextIndex.Value, 0, remainingCount - 1);
        }

        var deletedIndex = CurrentSession.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => string.Equals(pair.item.FilePath, deletedItem.FilePath, StringComparison.OrdinalIgnoreCase))
            .index;

        return Math.Min(deletedIndex, remainingCount - 1);
    }

    partial void OnSelectedThumbnailChanged(ThumbnailItemViewModel? value)
    {
        if (value is not null && value.Index != CurrentSession.CurrentIndex)
        {
            _ = NavigateToAsync(value.Index);
        }
    }

    partial void OnCurrentBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasImage));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnCurrentSessionChanged(BrowserSession value)
    {
        NotifyStateChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyStateChanged();
    }

    partial void OnIsDeletingChanged(bool value)
    {
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CurrentItem));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanDelete));
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSidebarVisible));
    }

    private static string BuildStatus(ImageEntry item, Bitmap? bitmap, int index, int totalCount)
    {
        var sizeText = bitmap is not null
            ? $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}"
            : item.PixelWidth.HasValue && item.PixelHeight.HasValue
            ? $"{item.PixelWidth} × {item.PixelHeight}"
            : "Unknown size";
        var fileSize = item.FileSizeBytes >= 1024 * 1024
            ? $"{item.FileSizeBytes / 1024d / 1024d:F1} MB"
            : $"{item.FileSizeBytes / 1024d:F0} KB";

        return $"{index + 1}/{totalCount} · {sizeText} · {fileSize}";
    }

    private static string BuildDeleteFailureMessage(DeleteResult result)
    {
        return BuildDeleteFailureMessage(result.ErrorMessage, result.DeletedPaths.Count);
    }

    private static string BuildPartialDeleteWarning(string? errorMessage)
    {
        return BuildDeleteFailureMessage(errorMessage, 1);
    }

    private static string BuildDeleteFailureMessage(string? errorMessage, int deletedPathCount)
    {
        var prefix = deletedPathCount > 0
            ? "Some files have been moved to the Recycle Bin, but could not complete the full deletion."
            : "Delete failed.";

        return string.IsNullOrWhiteSpace(errorMessage)
            ? prefix
            : $"{prefix} {errorMessage}";
    }
}
