namespace RawV.Models;

public sealed class BrowserSession
{
    public BrowserSession(IReadOnlyList<ImageEntry> items, int currentIndex)
    {
        Items = items;
        CurrentIndex = currentIndex;
    }

    public IReadOnlyList<ImageEntry> Items { get; }

    public int CurrentIndex { get; }

    public bool HasItems => Items.Count > 0;
}
