using CommunityToolkit.Mvvm.ComponentModel;

namespace filerename.ViewModels;

public partial class FileItemViewModel(string fullPath, string originalName) : ObservableObject
{
    [ObservableProperty]
    private string _originalName = originalName;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private bool _isChecked = true;

    public string FullPath { get; set; } = fullPath;
}
