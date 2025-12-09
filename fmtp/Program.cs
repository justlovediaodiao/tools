using MediaDevices;


class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: <local path> <MTP device path>");
            return;
        }

        var (localDir, mtpDir) = (args[0], args[1]);
        const string targetDir = "diff";

        MediaDevice? device = null;
        try
        {
            if (!Path.Exists(localDir))
            {
                Console.WriteLine($"Local directory not found: {localDir}");
                return;
            }

            device = MediaDevice.GetDevices().FirstOrDefault();
            
            if (device is null)
            {
                Console.WriteLine("No MTP device found");
                return;
            }

            Console.WriteLine($"Connect to MTP device: {device.FriendlyName}");
            device.Connect();

            if (!device.DirectoryExists(mtpDir))
            {
                Console.WriteLine($"MTP device directory not found: {mtpDir}");
                return;
            }

            Directory.CreateDirectory(targetDir);

            HashSet<string> localFiles = [.. Directory.GetFiles(localDir)
                                .Select(Path.GetFileName)
                                .OfType<string>()];

            var mtpFiles = device.GetDirectoryInfo(mtpDir)
                            .EnumerateFileSystemInfos()
                            .OfType<MediaFileInfo>()
                            .Where(f => !f.Name.StartsWith('.'))
                            .ToList();

            var filesToMove = localFiles.Except(mtpFiles.Select(f => f.Name), StringComparer.OrdinalIgnoreCase).ToList();
            MoveToDiff(localDir, targetDir, filesToMove);

            var filesToCopy = mtpFiles.Where(f => !localFiles.Contains(f.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            CopyFromMtp(device, localDir, filesToCopy);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            device?.Disconnect();
        }
    }

    static void MoveToDiff(string localDir, string targetDir, List<string> filesToMove)
    {
        if (filesToMove.Count == 0) return;
        
        Console.WriteLine($"Found {filesToMove.Count} file(s) to move to diff.");
        foreach (var fileName in filesToMove)
        {
            var sourcePath = Path.Combine(localDir, fileName);
            var destPath = Path.Combine(targetDir, fileName);

            File.Move(sourcePath, destPath, overwrite: true);
            Console.WriteLine(fileName);
        }
        Console.WriteLine($"Moved {filesToMove.Count} file(s) to {targetDir}");
    }

    static void CopyFromMtp(MediaDevice device, string localDir, List<MediaFileInfo> filesToCopy)
    {
        if (filesToCopy.Count == 0) return;

        Console.WriteLine($"Found {filesToCopy.Count} file(s) to copy from MTP.");
        
        var totalBytes = filesToCopy.Sum(f => (long)f.Length);
        var copiedBytes = 0L;
        
        DrawProgressBar(0, totalBytes, "Starting...");

        foreach (var file in filesToCopy)
        {
            var destPath = Path.Combine(localDir, file.Name);
            
            try 
            {
                DrawProgressBar(copiedBytes, totalBytes, file.Name);
                device.DownloadFile(file.FullName, destPath);
                copiedBytes += (long)file.Length;
                DrawProgressBar(copiedBytes, totalBytes, file.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Failed to download {file.Name}: {ex.Message}");
                DrawProgressBar(copiedBytes, totalBytes, "");
            }
        }
        
        Console.WriteLine();
        Console.WriteLine($"Copied {filesToCopy.Count} file(s) from MTP.");
    }

    static void DrawProgressBar(long current, long total, string message = "")
    {
        if (total == 0) return;
        
        const int width = 50;
        var percentage = (double)current / total;
        var filled = (int)(percentage * width);
        
        Console.Write($"\r[{new string('#', filled)}{new string('-', width - filled)}] {percentage:P0} {message[..Math.Min(message.Length, 40)].PadRight(40)}");
    }
}