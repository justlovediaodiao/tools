using System;
using System.IO;

namespace Ass;

public class Ass
{
    private string _file;
    private string[] _lines;

    public Ass(string file)
    {
        _file = file;
        _lines = File.ReadAllLines(file);
    }

    private void Timeline(int millisecond, int start, int end)
    {
        int startCol;
        int endCol;
        int startIndex;
        int endIndex;
        var index = EventsLine(out startCol, out endCol);
        for (int i = index; i < _lines.Length; i++)
        {
            if (_lines[i].StartsWith("Dialogue:"))
            {
                //starttime
                var time = GetTime(_lines[i], startCol, out startIndex, out endIndex);
                //is time between the start and end time
                if (start > 0 && time < start)
                    continue;
                if (end > 0 && time >= end)
                    continue;
                time += millisecond;
                _lines[i] = _lines[i].Substring(0, startIndex) + new TimeSpan(0, 0, 0, 0, time).ToString(@"h\:mm\:ss\.ff") + _lines[i].Substring(endIndex + 1);
                //endtime
                time = GetTime(_lines[i], endCol, out startIndex, out endIndex);
                time += millisecond;
                _lines[i] = _lines[i].Substring(0, startIndex) + new TimeSpan(0, 0, 0, 0, time).ToString(@"h\:mm\:ss\.ff") + _lines[i].Substring(endIndex + 1);
            }
        }
    }

    private int EventsLine(out int start, out int end)
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
        var columns = _lines[number + 1].Split(new char[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
        start = -1;
        end = -1;
        for (int i = 1; i < columns.Length; i++)
        {
            if (columns[i].Trim() == "Start")
                start = i - 1;
            else if (columns[i].Trim() == "End")
                end = i - 1;
        }
        if (start == -1 || end == -1)
            throw new FormatException("can not locate the Start and End column");
        return number + 1;
    }

    private int GetTime(string line, int colIndex, out int start, out int end)
    {
        start = -1;
        end = -1;
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
        var timeString = line.Substring(start, end - start + 1);
        return (int)TimeSpan.Parse(timeString).TotalMilliseconds;
    }

    public void Delay(int millisecond)
    {
        Timeline(millisecond, 0, 0);
    }

    public void Hurry(int millisecond)
    {
        Timeline(-millisecond, 0, 0);
    }

    public void Save()
    {
        var name = Path.GetFileNameWithoutExtension(_file);
        var ext = Path.GetExtension(_file);
        var filename = Path.Combine(Path.GetDirectoryName(_file), $"{name}_fix{ext}");
        File.WriteAllLines(filename, _lines);
    }
}
