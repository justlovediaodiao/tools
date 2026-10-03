using Avalonia.Media.Imaging;

namespace RawV.Services;

public sealed class ThumbnailService(ImageLoaderService imageLoader) : IDisposable
{
    private int _generation;
    private readonly Dictionary<string, int> _fileVersions = new(StringComparer.OrdinalIgnoreCase);
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

        var generation = _generation;
        var fileVersion = _fileVersions.GetValueOrDefault(filePath);
        var bitmap = await LoadThumbnailAsync(filePath, width, height, cancellationToken);
        if (generation != _generation || fileVersion != _fileVersions.GetValueOrDefault(filePath))
        {
            bitmap?.Dispose();
            return null;
        }

        if (bitmap is not null)
        {
            _cache[cacheKey] = bitmap;
        }

        return bitmap;
    }

    public void InvalidateCache(string filePath)
    {
        _fileVersions[filePath] = _fileVersions.GetValueOrDefault(filePath) + 1;
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
        _generation++;
        _fileVersions.Clear();
        foreach (var bitmap in _cache.Values)
        {
            bitmap?.Dispose();
        }
        _cache.Clear();
    }

    private async Task<Bitmap?> LoadThumbnailAsync(string filePath, int targetWidth, int targetHeight, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await imageLoader.LoadAsync(filePath, cancellationToken);
            var original = image.Bitmap;
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var size = original.PixelSize;
                var scale = Math.Min(1, Math.Min((double)targetWidth / size.Width, (double)targetHeight / size.Height));
                // Always create an independently owned thumbnail, including small images.
                return original.CreateScaledBitmap(new Avalonia.PixelSize(
                    Math.Max(1, (int)(size.Width * scale)), Math.Max(1, (int)(size.Height * scale))));
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
