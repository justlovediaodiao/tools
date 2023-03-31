using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FileReName;

class FileName
{
    public static string PreviewRename(string fileName, string spliter, string rule)
    {   
        // MessageBox.Show(fileName + "\n" + spliter + "\n" + rule);
        var strArr = fileName.Split(spliter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var regex = new Regex(@"\{\d+\}");
        var newName = rule;
        foreach (Match match in regex.Matches(rule))
        {
            var index = int.Parse(match.Value.Substring(1, match.Value.Length - 2));
            if (index >= strArr.Length)
                return string.Empty;
            newName = newName.Replace(match.Value, strArr[index]);
        }
        if (!newName.Contains('.'))
        {
            newName = newName + Path.GetExtension(fileName);
        }
        return newName;
    }

    public static void Rename(string source, string src)
    {
        var dir = Path.GetDirectoryName(source);
        var targetName = Path.Combine(dir, src);
        File.Move(source, targetName);
    }
}
