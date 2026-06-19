using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PIDMobileSpeaker;

public static class Csv
{
    public static IEnumerable<Dictionary<string, string>> ReadRows(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var headerLine = reader.ReadLine();
        if (headerLine == null) yield break;
        var headers = ParseLine(headerLine);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Equals(headerLine, StringComparison.OrdinalIgnoreCase)) continue;
            var values = ParseLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = i < values.Count ? values[i] : "";
            yield return row;
        }
    }

    private static List<string> ParseLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else sb.Append(ch);
            }
            else
            {
                if (ch == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (ch == '"') quoted = true;
                else sb.Append(ch);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    public static string Get(this Dictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value : "";
}
