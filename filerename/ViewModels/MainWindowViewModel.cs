using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using filerename.Services;

namespace filerename.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public class PreviewPart
    {
        public string Text { get; set; } = string.Empty;
        public IBrush Foreground { get; set; } = new SolidColorBrush(Colors.Gray);
    }

    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private string _separator = string.Empty;

    [ObservableProperty]
    private string _rule = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PreviewPart> _splitPreviewParts = new();

    partial void OnSeparatorChanged(string value)
    {
        UpdateSplitPreview();
    }

    private void UpdateSplitPreview()
    {
        SplitPreviewParts.Clear();

        if (string.IsNullOrWhiteSpace(Separator) || Files.Count == 0)
        {
            return;
        }

        var firstFile = Files[0].OriginalName;
        var parts = firstFile.Split(Separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        // Use fluent colors - softer blue
        var accentBrush = new SolidColorBrush(Color.Parse("#0078D4"));
        var grayBrush = new SolidColorBrush(Color.Parse("#6E6E6E"));
        var textBrush = new SolidColorBrush(Colors.Black);

        SplitPreviewParts.Add(new PreviewPart
        {
            Text = "Separator: ",
            Foreground = grayBrush
        });

        for (int i = 0; i < parts.Length; i++)
        {
            SplitPreviewParts.Add(new PreviewPart
            {
                Text = $"{{{i}}}",
                Foreground = accentBrush
            });

            SplitPreviewParts.Add(new PreviewPart
            {
                Text = "=",
                Foreground = grayBrush
            });

            SplitPreviewParts.Add(new PreviewPart
            {
                Text = parts[i],
                Foreground = textBrush
            });

            if (i < parts.Length - 1)
            {
                SplitPreviewParts.Add(new PreviewPart
                {
                    Text = " ",
                    Foreground = grayBrush
                });
            }
        }
    }

    public const string READY = "ready";
    public const string SKIP = "skip";
    public const string SUCCESS = "success";
    public const string ERROR = "error";

    [RelayCommand]
    private async Task AddFiles(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Select Files"
        });

        foreach (var file in files)
        {
            AddFile(file.Path.LocalPath);
        }
    }

    [RelayCommand]
    private async Task AddFolder(IStorageProvider storageProvider)
    {
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folderPath = folders[0].Path.LocalPath;
            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                AddFile(file);
            }
        }
    }

    public void AddFile(string filePath)
    {
        if (!Files.Any(f => f.FullPath == filePath))
        {
            Files.Add(new FileItemViewModel(filePath, Path.GetFileName(filePath)));
            UpdateSplitPreview();
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var itemsToRemove = Files.Where(f => f.IsChecked).ToList();
        foreach (var item in itemsToRemove)
        {
            Files.Remove(item);
        }
        UpdateSplitPreview();
    }

    [RelayCommand]
    private void SelectAll()
    {
        // If all files are already selected, reverse the selection
        // Otherwise, select all files
        if (Files.Count > 0 && Files.All(f => f.IsChecked))
        {
            foreach (var file in Files)
            {
                file.IsChecked = !file.IsChecked;
            }
        }
        else
        {
            foreach (var file in Files)
            {
                file.IsChecked = true;
            }
        }
    }

    [RelayCommand]
    private void Preview()
    {
        if (string.IsNullOrWhiteSpace(Separator) || string.IsNullOrWhiteSpace(Rule))
        {
            // In a real app, we might want to show a message.
            // For now, we just return or maybe set a status property.
            return;
        }

        foreach (var item in Files)
        {
            if (!item.IsChecked || item.Status == SUCCESS) continue;

            var newName = FileName.PreviewRename(item.OriginalName, Separator, Rule);
            if (!string.IsNullOrEmpty(newName))
            {
                item.Status = READY;
                item.NewName = newName;
            }
            else
            {
                item.Status = SKIP;
                item.NewName = string.Empty;
            }
        }
    }

    [RelayCommand]
    private void Start()
    {
        Preview(); // Ensure latest preview

        // Check if we have valid rules
        if (string.IsNullOrWhiteSpace(Separator) || string.IsNullOrWhiteSpace(Rule))
        {
             return;
        }

        foreach (var item in Files)
        {
            if (item.IsChecked && item.Status == READY && item.OriginalName != item.NewName)
            {
                try
                {
                    FileName.Rename(item.FullPath, item.NewName);
                    item.Status = SUCCESS;
                    // Update FullPath and OriginalName if we want to allow further renaming?
                    // The original app didn't seem to update them immediately for re-renaming,
                    // but it marked status as SUCCESS.
                }
                catch (Exception ex)
                {
                    item.Status = ERROR;
                    item.NewName = ex.Message;
                }
            }
        }
    }
}
