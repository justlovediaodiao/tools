using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FileReName;

class FileName
{
    public static string PreviewRename(string fileName, string sep, string rule)
    {   
        var arr = fileName.Split(sep.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var regex = new Regex(@"\{\d+\}");
        var newName = rule;
        foreach (Match match in regex.Matches(rule))
        {
            var index = int.Parse(match.Value.Substring(1, match.Value.Length - 2));
            if (index >= arr.Length)
                return string.Empty;
            newName = newName.Replace(match.Value, arr[index]);
        }
        if (!newName.Contains('.'))
        {
            newName = newName + Path.GetExtension(fileName);
        }
        return newName;
    }

    public static void Rename(string source, string target)
    {
        var dir = Path.GetDirectoryName(source);
        var targetName = Path.Combine(dir, target);
        File.Move(source, targetName);
    }
}
