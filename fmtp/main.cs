using System;
using System.IO;
using System.Linq;
using MediaDevices;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("usage: <local path> <MTP device path>");
            return;
        }

        string localDir = args[0];
        string mtpDir = args[1];
        string targetDir = "diff";

        MediaDevice device = null;

        try
        {
            if (!Directory.Exists(localDir))
            {
                Console.WriteLine($"local directory not found: {localDir}");
                return;
            }

            device = MediaDevice.GetDevices().FirstOrDefault();
            
            if (device == null)
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
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var mtpFiles = device.GetDirectoryInfo(mtpDir)
                            .EnumerateFileSystemInfos()
                            .Where(f => f is MediaFileInfo)
                            .Select(f => f.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var uniqueFiles = localFiles.Except(mtpFiles);

            int movedCount = 0;
            foreach (var fileName in uniqueFiles)
            {
                string sourcePath = Path.Combine(localDir, fileName);
                string destPath = Path.Combine(targetDir, fileName);

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
            if (device != null) {
                device.Disconnect();
            }
        }
    }
}