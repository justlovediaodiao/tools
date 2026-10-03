using Avalonia.Media.Imaging;

namespace RawV.Services;

// Cache access and lease disposal run on the UI thread; only decoding runs off-thread.
public sealed class ImageLoaderService : IDisposable
{
    public const int CacheCapacity = 15;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recent = new();
    private bool _disposed;

    public async Task<ImageLease> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var key = Path.GetFullPath(filePath);
        if (!_cache.TryGetValue(key, out var entry))
        {
            entry = new CacheEntry(Task.Run(() => new Bitmap(key)), _recent.AddFirst(key));
            _cache.Add(key, entry);
        }
        else
        {
            _recent.Remove(entry.Node);
            _recent.AddFirst(entry.Node);
        }

        // Each awaiting consumer retains the bitmap even if its entry is evicted.
        entry.Users++;
        while (_cache.Count > CacheCapacity)
        {
            InvalidateCache(_recent.Last!.Value);
        }

        try
        {
            var bitmap = await entry.Load;
            cancellationToken.ThrowIfCancellationRequested();
            return new ImageLease(bitmap, () => Release(entry));
        }
        catch
        {
            if (_cache.TryGetValue(key, out var cached) && ReferenceEquals(cached, entry))
                InvalidateCache(key);
            Release(entry);
            throw;
        }
    }

    public void InvalidateCache(string filePath)
    {
        if (!_cache.Remove(Path.GetFullPath(filePath), out var entry)) return;
        _recent.Remove(entry.Node);
        entry.Cached = false;
        DisposeIfUnused(entry);
    }

    public void ClearCache()
    {
        foreach (var entry in _cache.Values)
        {
            entry.Cached = false;
            DisposeIfUnused(entry);
        }
        _cache.Clear();
        _recent.Clear();
    }

    private static void Release(CacheEntry entry)
    {
        entry.Users--;
        DisposeIfUnused(entry);
    }

    private static void DisposeIfUnused(CacheEntry entry)
    {
        if (!entry.Cached && entry.Users == 0 && entry.Load.IsCompletedSuccessfully)
            entry.Load.Result.Dispose();
    }

    public void Dispose()
    {
        _disposed = true;
        ClearCache();
    }

    private sealed class CacheEntry(Task<Bitmap> load, LinkedListNode<string> node)
    {
        public Task<Bitmap> Load { get; } = load;
        public LinkedListNode<string> Node { get; } = node;
        public bool Cached { get; set; } = true;
        public int Users { get; set; }
    }

    public sealed class ImageLease(Bitmap bitmap, Action release) : IDisposable
    {
        private Action? _release = release;
        public Bitmap Bitmap { get; } = bitmap;

        public void Dispose()
        {
            var release = _release;
            _release = null;
            release?.Invoke();
        }
    }
}
