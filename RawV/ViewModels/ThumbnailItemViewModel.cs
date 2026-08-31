using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RawV.Services;

namespace RawV.ViewModels;

public partial class ThumbnailItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private Bitmap? thumbnail;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isLoaded;

    [ObservableProperty]
    private bool isLoading;

    public int Index { get; set; }

    public async Task LoadAsync(ThumbnailService thumbnailService, int width, int height, CancellationToken cancellationToken = default)
    {
        if (IsLoaded || IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            Thumbnail = await thumbnailService.GetThumbnailAsync(FilePath, width, height, cancellationToken);
        }
        catch
        {
            // On failure, Thumbnail remains null and gray placeholder is shown
        }
        finally
        {
            IsLoading = false;
            IsLoaded = true;
        }
    }
}
