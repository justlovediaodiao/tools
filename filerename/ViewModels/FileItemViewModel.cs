using CommunityToolkit.Mvvm.ComponentModel;

namespace filerename.ViewModels;

public partial class FileItemViewModel(string fullPath, string originalName) : ObservableObject
{
    [ObservableProperty]
    private string _originalName = originalName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private bool _isChecked = true;

    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsReady => Status == MainWindowViewModel.READY;
    public bool IsSuccess => Status == MainWindowViewModel.SUCCESS;
    public bool IsError => Status == MainWindowViewModel.ERROR;

    public string FullPath { get; set; } = fullPath;
}
