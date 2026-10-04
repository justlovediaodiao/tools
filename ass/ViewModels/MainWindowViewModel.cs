using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ass.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public const string READY = "ready";
    public const string SUCCESS = "success";
    public const string ERROR = "error";

    private static readonly FilePickerFileType AssFileType = new("ASS subtitles")
    {
        Patterns = ["*.ass"]
    };
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public ObservableCollection<FileItemViewModel> Files { get; } = [];

    public MainWindowViewModel()
    {
        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(OutputFolderHint));
    }

    [ObservableProperty]
    private string _offset = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFolderHint))]
    [NotifyPropertyChangedFor(nameof(HasOutputFolder))]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChooseOutputCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;
    public bool HasOutputFolder => !string.IsNullOrEmpty(OutputFolder);
    public string OutputFolderHint
    {
        get
        {
            if (!string.IsNullOrEmpty(OutputFolder)) return OutputFolder;
            var directories = Files.Select(file => Path.GetDirectoryName(file.FullPath)!)
                .Distinct(PathComparer).ToArray();
            return directories.Length == 0 ? "Default Source directory"
                : string.Join(Environment.NewLine, directories);
        }
    }

    partial void OnOffsetChanged(string value)
    {
        foreach (var file in Files) file.ClearPreview();
    }

    private bool CanChoose() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task OpenFile(IStorageProvider storageProvider)
    {
        try
        {
            IsBusy = true;
            var selected = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select ASS subtitles", AllowMultiple = true,
                FileTypeFilter = [AssFileType]
            });
            if (selected.Count == 0) return;
            var paths = selected.Select(file => file.TryGetLocalPath()
                    ?? throw new IOException("Choose subtitles stored on this computer."))
                .Select(Path.GetFullPath).Distinct(PathComparer).ToArray();
            foreach (var path in paths)
                if (!Files.Any(file => PathComparer.Equals(file.FullPath, path)))
                    Files.Add(new FileItemViewModel(path));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseOutput(IStorageProvider storageProvider)
    {
        try
        {
            IsBusy = true;
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose output folder", AllowMultiple = false
            });
            if (folders.Count > 0)
                OutputFolder = folders[0].TryGetLocalPath()
                    ?? throw new IOException("Choose an output folder on this computer.");
        }
        finally { IsBusy = false; }
    }

    private int GetOffset()
    {
        if (!int.TryParse(Offset, out var milliseconds))
            throw new FormatException("Enter a whole number of milliseconds, for example 500 or -500.");
        return milliseconds;
    }

    private static (Ass Subtitle, (string OriginalStart, string AdjustedStart) Sample)
        Prepare(FileItemViewModel file, int milliseconds)
    {
        var subtitle = new Ass(file.FullPath);
        var sample = subtitle.Adjust(milliseconds)
            ?? throw new FormatException("No Dialogue lines found.");
        return (subtitle, sample);
    }

    private static async Task<Ass> PrepareFile(FileItemViewModel file, int milliseconds)
    {
        file.ClearPreview();
        var prepared = await Task.Run(() => Prepare(file, milliseconds));
        file.OriginalStart = prepared.Sample.OriginalStart;
        file.AdjustedStart = prepared.Sample.AdjustedStart;
        file.Status = READY;
        return prepared.Subtitle;
    }

    private async Task PreviewFiles(FileItemViewModel[] pending, int milliseconds)
    {
        foreach (var file in pending)
        {
            try
            {
                await PrepareFile(file, milliseconds);
            }
            catch (Exception exception)
            {
                file.Status = ERROR;
                file.Result = exception.Message;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Preview()
    {
        if (IsBusy) return;
        var pending = Files.Where(file => !file.IsSuccess).ToArray();
        if (pending.Length == 0) return;
        try
        {
            var milliseconds = GetOffset();
            IsBusy = true;
            await PreviewFiles(pending, milliseconds);
        }
        catch (Exception exception) { SetErrors(pending, exception); }
        finally { IsBusy = false; }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Start()
    {
        if (IsBusy) return;
        var pending = Files.Where(file => !file.IsSuccess).ToArray();
        if (pending.Length == 0) return;
        try
        {
            var milliseconds = GetOffset();
            if (milliseconds == 0) return;
            if (!string.IsNullOrEmpty(OutputFolder) && !Directory.Exists(OutputFolder))
                throw new DirectoryNotFoundException("Output folder no longer exists.");
            IsBusy = true;
            var folder = OutputFolder;
            var sources = Files.Select(file => CanonicalFilePath(file.FullPath))
                .ToHashSet(PathComparer);
            foreach (var file in pending)
            {
                try
                {
                    var subtitle = await PrepareFile(file, milliseconds);
                    var result = await Task.Run(() =>
                    {
                        var output = GetOutputPath(file.FullPath, folder);
                        if (sources.Contains(CanonicalFilePath(output)))
                            throw new IOException("Output would overwrite an input subtitle.");
                        subtitle.Save(output);
                        return output;
                    });
                    file.Status = SUCCESS;
                    file.Result = result;
                }
                catch (Exception exception)
                {
                    file.Status = ERROR;
                    file.Result = exception.Message;
                }
            }
        }
        catch (Exception exception) { SetErrors(pending, exception); }
        finally { IsBusy = false; }
    }

    private static void SetErrors(IEnumerable<FileItemViewModel> files, Exception exception)
    {
        foreach (var file in files.Where(file => !file.IsSuccess))
        {
            file.ClearPreview();
            file.Status = ERROR;
            file.Result = exception.Message;
        }
    }

    private static string GetOutputPath(string source, string outputFolder)
    {
        var sourceFolder = Path.GetDirectoryName(source)!;
        var folder = string.IsNullOrEmpty(outputFolder) ? sourceFolder : outputFolder;
        var sameFolder = PathComparer.Equals(CanonicalDirectory(sourceFolder), CanonicalDirectory(folder));
        var extension = Path.GetExtension(source);
        var stem = Path.GetFileNameWithoutExtension(source) + (sameFolder ? "_adjusted" : "");
        return Path.Combine(folder, stem + extension);
    }

    private static string CanonicalFilePath(string path)
    {
        var target = File.Exists(path) ? new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true) : null;
        var fullPath = target?.FullName ?? Path.GetFullPath(path);
        return Path.Combine(CanonicalDirectory(Path.GetDirectoryName(fullPath)!), Path.GetFileName(fullPath));
    }

    private static string CanonicalDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        var target = directory.Exists ? directory.ResolveLinkTarget(returnFinalTarget: true) : null;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            target?.FullName ?? directory.FullName));
    }
}
