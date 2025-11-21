namespace Ass;

public class Ass(string file)
{
    private readonly string _file = file;
    private string[] _lines = File.ReadAllLines(file);

    private void Timeline(int millisecond, int start, int end)
    {
        var (startCol, endCol, index) = EventsLine();
        for (int i = index; i < _lines.Length; i++)
        {
            if (_lines[i].StartsWith("Dialogue:"))
            {
                //starttime
                var (time, startIndex, endIndex) = GetTime(_lines[i], startCol);
                //is time between the start and end time
                if (start > 0 && time < start)
                    continue;
                if (end > 0 && time >= end)
                    continue;
                time += millisecond;
                _lines[i] = _lines[i][..startIndex] + new TimeSpan(0, 0, 0, 0, time).ToString(@"h\:mm\:ss\.ff") + _lines[i][(endIndex + 1)..];
                //endtime
                (time, startIndex, endIndex) = GetTime(_lines[i], endCol);
                time += millisecond;
                _lines[i] = _lines[i][..startIndex] + new TimeSpan(0, 0, 0, 0, time).ToString(@"h\:mm\:ss\.ff") + _lines[i][(endIndex + 1)..];
            }
        }
    }

    private (int StartCol, int EndCol, int Index) EventsLine()
    {
        //find the events and format line
        var number = -1;
        for (int i = 0; i < _lines.Length; i++)
        {
            if (_lines[i].StartsWith("[Events]"))
            {
                number = i;
                break;
            }
        }
        if (number == -1)
            throw new FormatException("[Events] section not found");
        if (number == _lines.Length - 1 || !_lines[number + 1].StartsWith("Format:"))
            throw new FormatException("Format tag not found");
        //find the start and end column
        var columns = _lines[number + 1].Split([':', ','], StringSplitOptions.RemoveEmptyEntries);
        var start = -1;
        var end = -1;
        for (int i = 1; i < columns.Length; i++)
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
            while (line[start] == ' ')
                ++start;
        }
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == ',')
            {
                if (start != -1)
                {
                    end = i - 1;
                    break;
                }
                else
                {
                    if (--colIndex == 0)
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

    public void Delay(int millisecond) => Timeline(millisecond, 0, 0);

    public void Hurry(int millisecond) => Timeline(-millisecond, 0, 0);

    public void Save()
    {
        var name = Path.GetFileNameWithoutExtension(_file);
        var ext = Path.GetExtension(_file);
        var filename = Path.Combine(Path.GetDirectoryName(_file) ?? "", $"{name}_fix{ext}");
        File.WriteAllLines(filename, _lines);
    }
}
