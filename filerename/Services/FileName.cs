using System.Text.RegularExpressions;

namespace filerename.Services;

public partial class FileName
{
    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();

    public static string PreviewRename(string fileName, string sep, string rule)
    {   
        var arr = fileName.Split(sep.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var newName = rule;
        foreach (Match match in PlaceholderRegex().Matches(rule))
        {
            var index = int.Parse(match.Value[1..^1]);
            if (index >= arr.Length)
                return string.Empty;
            newName = newName.Replace(match.Value, arr[index]);
        }
        if (!newName.Contains('.'))
        {
            newName += Path.GetExtension(fileName);
        }
        return newName;
    }

    public static void Rename(string source, string target)
    {
        var dir = Path.GetDirectoryName(source);
        if (dir is null) return;
        var targetName = Path.Combine(dir, target);
        File.Move(source, targetName);
    }
}
