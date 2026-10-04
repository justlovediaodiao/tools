using CommunityToolkit.Mvvm.ComponentModel;

namespace Ass.ViewModels;

public partial class FileItemViewModel(string fullPath) : ObservableObject
{
    public string FullPath { get; } = fullPath;
    public string Name => Path.GetFileName(FullPath);
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsReady => Status == MainWindowViewModel.READY;
    public bool IsSuccess => Status == MainWindowViewModel.SUCCESS;
    public bool IsError => Status == MainWindowViewModel.ERROR;

    [ObservableProperty]
    private string _originalStart = string.Empty;

    [ObservableProperty]
    private string _adjustedStart = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(IsSuccess))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    public void ClearPreview()
    {
        if (IsSuccess) return;
        OriginalStart = string.Empty;
        AdjustedStart = string.Empty;
        Status = string.Empty;
        Result = string.Empty;
    }
}
