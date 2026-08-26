using System.Text;

namespace Ass;

public class Ass
{
    private readonly Encoding _encoding;
    private readonly string[] _lines;

    public Ass(string file)
    {
        using var reader = new StreamReader(file, true);
        List<string> lines = [];
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        _encoding = reader.CurrentEncoding;
        _lines = [.. lines];
    }

    public void Adjust(int milliseconds)
    {
        var (startCol, endCol, index) = EventsLine();
        for (var i = index; i < _lines.Length; i++)
        {
            if (_lines[i].StartsWith("Dialogue:", StringComparison.Ordinal))
            {
                // start time
                var (time, startIndex, endIndex) = GetTime(_lines[i], startCol);
                time += milliseconds;
                _lines[i] = _lines[i][..startIndex] + FormatTime(time) + _lines[i][(endIndex + 1)..];

                // end time
                (time, startIndex, endIndex) = GetTime(_lines[i], endCol);
                time += milliseconds;
                _lines[i] = _lines[i][..startIndex] + FormatTime(time) + _lines[i][(endIndex + 1)..];
            }
        }
    }

    private (int StartCol, int EndCol, int Index) EventsLine()
    {
        // find the events and format line
        var number = -1;
        for (var i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].StartsWith("[Events]", StringComparison.Ordinal))
            {
                number = i;
                break;
            }
        }
        if (number == -1)
            throw new FormatException("[Events] section not found");
        if (number == _lines.Length - 1 || !_lines[number + 1].StartsWith("Format:", StringComparison.Ordinal))
            throw new FormatException("Format tag not found");

        // find the start and end column
        var columns = _lines[number + 1].Split([':', ','], StringSplitOptions.RemoveEmptyEntries);
        var start = -1;
        var end = -1;
        for (var i = 1; i < columns.Length; i++)
        {
            if (columns[i].Trim() == "Start")
                start = i - 1;
            else if (columns[i].Trim() == "End")
                end = i - 1;
        }
        if (start == -1 || end == -1)
            throw new FormatException("can not locate the Start and End column");
        return (start, end, number + 1);
    }

    private (int Time, int StartIndex, int EndIndex) GetTime(string line, int colIndex)
    {
        var start = -1;
        var end = -1;
        if (colIndex == 0)
        {
            start = line.IndexOf(':') + 1;
            while (start < line.Length && line[start] == ' ')
                ++start;
        }
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ',')
            {
                if (start != -1)
                {
                    end = i - 1;
                    break;
                }
                else if (--colIndex == 0)
                {
                    start = i + 1;
                }
            }
        }
        if (start == -1 || end == -1)
            throw new FormatException("error Dialogue line");
        var timeString = line[start..(end + 1)];
        var time = (int)TimeSpan.Parse(timeString).TotalMilliseconds;
        return (time, start, end);
    }

    private static string FormatTime(int milliseconds)
    {
        if (milliseconds < 0)
            throw new InvalidOperationException("The adjusted time cause negative timeline");

        return TimeSpan.FromMilliseconds(milliseconds).ToString(@"h\:mm\:ss\.ff");
    }

    public void Save(string outputFile) => File.WriteAllLines(outputFile, _lines, _encoding);
}
