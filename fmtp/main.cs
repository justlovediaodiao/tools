using MediaDevices;

if (args.Length != 2)
{
    Console.WriteLine("usage: <local path> <MTP device path>");
    return;
}

var localDir = args[0];
var mtpDir = args[1];
const string targetDir = "diff";

MediaDevice? device = null;

try
{
    if (!Directory.Exists(localDir))
    {
        Console.WriteLine($"local directory not found: {localDir}");
        return;
    }

    device = MediaDevice.GetDevices().FirstOrDefault();
    
    if (device is null)
    {
        Console.WriteLine("no MTP device found");
        return;
    }

    Console.WriteLine($"connect to MTP device: {device.FriendlyName}");
    device.Connect();

    if (!device.DirectoryExists(mtpDir))
    {
        Console.WriteLine($"MTP device directory not found: {mtpDir}");
        return;
    }

    Directory.CreateDirectory(targetDir);

    var localFiles = Directory.GetFiles(localDir)
                        .Select(Path.GetFileName)
                        .OfType<string>()
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var mtpFiles = device.GetDirectoryInfo(mtpDir)
                    .EnumerateFileSystemInfos()
                    .Where(f => f is MediaFileInfo)
                    .Select(f => f.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var uniqueFiles = localFiles.Except(mtpFiles);

    var movedCount = 0;
    foreach (var fileName in uniqueFiles)
    {
        var sourcePath = Path.Combine(localDir, fileName);
        var destPath = Path.Combine(targetDir, fileName);

        File.Move(sourcePath, destPath);
        Console.WriteLine($"move {fileName}");
        movedCount++;
    }
    Console.WriteLine($"moved {movedCount} files to {targetDir}");
}
catch (Exception ex)
{
    Console.WriteLine($"error: {ex.Message}");
}
finally
{
    device?.Disconnect();
}