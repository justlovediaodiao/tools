using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RawV.Models;
using RawV.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RawV.ViewModels;

public partial class FmtpWindowViewModel : ViewModelBase
{
    private readonly FmtpService _fmtpService = new();
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string localDirectory = string.Empty;

    [ObservableProperty]
    private string mtpPath = string.Empty;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private bool isActivityExpanded;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private int moveCurrent;

    [ObservableProperty]
    private int moveTotal;

    [ObservableProperty]
    private string moveFile = string.Empty;

    [ObservableProperty]
    private int copyCurrent;

    [ObservableProperty]
    private int copyTotal;

    [ObservableProperty]
    private string copyFile = string.Empty;

    public FmtpWindowViewModel(string? initialLocalDirectory = null)
    {
        LocalDirectory = initialLocalDirectory ?? string.Empty;
    }

    public ObservableCollection<string> LogEntries { get; } = new();

    public bool CanRun => !IsRunning
        && Directory.Exists(LocalDirectory)
        && !string.IsNullOrWhiteSpace(MtpPath);

    public bool CanCancel => IsRunning;

    public double MoveMaximum => Math.Max(1, MoveTotal);

    public double CopyMaximum => Math.Max(1, CopyTotal);

    public bool HasMoveProgress => MoveTotal > 0;

    public bool HasCopyProgress => CopyTotal > 0;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;
        IsRunning = true;
        StatusMessage = "Starting fmtp...";
        MoveCurrent = MoveTotal = CopyCurrent = CopyTotal = 0;
        MoveFile = CopyFile = string.Empty;
        LogEntries.Clear();

        var progress = new Progress<FmtpEvent>(HandleEvent);
        try
        {
            var exitCode = await _fmtpService.RunAsync(
                LocalDirectory.Trim(),
                MtpPath.Trim(),
                progress,
                cancellationTokenSource.Token);

            if (exitCode == 0)
            {
                StatusMessage = "Sync completed successfully.";
            }
            else
            {
                StatusMessage = $"fmtp exited with code {exitCode}.";
                LogEntries.Add($"[ERROR] {StatusMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sync canceled.";
            LogEntries.Add("[INFO] Sync canceled by user.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            StatusMessage = "Unable to run fmtp.";
            LogEntries.Add($"[ERROR] {ex.Message}");
        }
        finally
        {
            cancellationTokenSource.Dispose();
            if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
            {
                _cancellationTokenSource = null;
            }
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        StatusMessage = "Canceling...";
        _cancellationTokenSource?.Cancel();
    }

    public void RefreshCanRun() => NotifyCommandStateChanged();

    private void HandleEvent(FmtpEvent fmtpEvent)
    {
        switch (fmtpEvent.Type)
        {
            case "move":
                MoveCurrent = fmtpEvent.Current;
                MoveTotal = fmtpEvent.Total;
                MoveFile = fmtpEvent.File ?? string.Empty;
                StatusMessage = $"Moving local-only files ({MoveCurrent}/{MoveTotal})";
                LogEntries.Add($"[MOVE {MoveCurrent}/{MoveTotal}] {MoveFile}");
                break;
            case "copy":
                CopyCurrent = fmtpEvent.Current;
                CopyTotal = fmtpEvent.Total;
                CopyFile = fmtpEvent.File ?? string.Empty;
                StatusMessage = $"Copying from MTP ({CopyCurrent}/{CopyTotal})";
                LogEntries.Add($"[COPY {CopyCurrent}/{CopyTotal}] {CopyFile}");
                break;
            case "success":
                StatusMessage = fmtpEvent.Message ?? "Completed successfully.";
                LogEntries.Add($"[SUCCESS] {StatusMessage}");
                break;
            case "error":
                StatusMessage = fmtpEvent.Message ?? "fmtp reported an error.";
                LogEntries.Add($"[ERROR] {StatusMessage}");
                break;
            default:
                if (!string.IsNullOrWhiteSpace(fmtpEvent.Message))
                {
                    StatusMessage = fmtpEvent.Message;
                    LogEntries.Add($"[INFO] {fmtpEvent.Message}");
                }
                break;
        }
    }

    partial void OnLocalDirectoryChanged(string value) => NotifyCommandStateChanged();

    partial void OnMtpPathChanged(string value) => NotifyCommandStateChanged();

    partial void OnIsRunningChanged(bool value) => NotifyCommandStateChanged();

    partial void OnMoveTotalChanged(int value)
    {
        OnPropertyChanged(nameof(MoveMaximum));
        OnPropertyChanged(nameof(HasMoveProgress));
    }

    partial void OnCopyTotalChanged(int value)
    {
        OnPropertyChanged(nameof(CopyMaximum));
        OnPropertyChanged(nameof(HasCopyProgress));
    }

    private void NotifyCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanCancel));
        RunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

}
