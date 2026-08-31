using Avalonia.Media.Imaging;

namespace RawV.Services;

public sealed class ImageLoaderService
{
    public Task<Bitmap> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Bitmap(filePath);
        }, cancellationToken);
    }
}
