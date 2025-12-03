using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using filerename.Services;

namespace filerename.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private string _separator = string.Empty;

    [ObservableProperty]
    private string _rule = string.Empty;

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
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in Files)
        {
            file.IsChecked = true;
        }
    }

    [RelayCommand]
    private void ReverseSelect()
    {
        foreach (var file in Files)
        {
            file.IsChecked = !file.IsChecked;
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
