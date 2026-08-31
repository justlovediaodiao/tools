using Avalonia.Media.Imaging;

namespace RawV.Services;

public sealed class ThumbnailService : IDisposable
{
    private readonly Dictionary<string, Bitmap> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Bitmap?> GetThumbnailAsync(string filePath, int width, int height, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var cacheKey = $"{filePath}:{width}:{height}";

        if (_cache.TryGetValue(cacheKey, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        var bitmap = await LoadThumbnailAsync(filePath, width, height, cancellationToken);
        if (bitmap is not null)
        {
            _cache[cacheKey] = bitmap;
        }

        return bitmap;
    }

    public void InvalidateCache(string filePath)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(filePath + ":", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var key in keysToRemove)
        {
            if (_cache.TryGetValue(key, out var bitmap))
            {
                _cache.Remove(key);
                bitmap?.Dispose();
            }
        }
    }

    public void ClearCache()
    {
        foreach (var bitmap in _cache.Values)
        {
            bitmap?.Dispose();
        }
        _cache.Clear();
    }

    private static async Task<Bitmap?> LoadThumbnailAsync(string filePath, int targetWidth, int targetHeight, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var fileStream = File.OpenRead(filePath);
                var original = new Bitmap(fileStream);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var originalWidth = original.PixelSize.Width;
                    var originalHeight = original.PixelSize.Height;

                    if (originalWidth <= targetWidth && originalHeight <= targetHeight)
                    {
                        return original;
                    }

                    var scale = Math.Min((double)targetWidth / originalWidth, (double)targetHeight / originalHeight);
                    var newWidth = (int)(originalWidth * scale);
                    var newHeight = (int)(originalHeight * scale);

                    var resized = original.CreateScaledBitmap(new Avalonia.PixelSize(newWidth, newHeight));
                    original.Dispose();
                    return resized;
                }
                catch
                {
                    original.Dispose();
                    throw;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => ClearCache();
}
