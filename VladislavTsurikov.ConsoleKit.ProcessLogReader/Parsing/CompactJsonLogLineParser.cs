using System.Text.Json;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

[LogLineParser(order: 10)]
public sealed class CompactJsonLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLine)
    {
        parsedLine = null;
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            DateTimeOffset timestamp = ReadTimestamp(root);
            LogSeverity severity = ReadSeverity(root);
            string message = ReadMessage(root);
            string detail = ReadDetail(root, line);
            parsedLine = new ParsedLogLine(timestamp, severity, message, detail);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DateTimeOffset ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("@t", out JsonElement timestampElement) &&
            DateTimeOffset.TryParse(timestampElement.GetString(), out DateTimeOffset timestamp))
        {
            return timestamp;
        }

        return DateTimeOffset.Now;
    }

    private static LogSeverity ReadSeverity(JsonElement root)
    {
        string level = root.TryGetProperty("@l", out JsonElement levelElement)
            ? levelElement.GetString() ?? "Information"
            : "Information";

        return level.ToLowerInvariant() switch
        {
            "warning" => LogSeverity.Warning,
            "error" => LogSeverity.Error,
            "fatal" => LogSeverity.Error,
            _ => LogSeverity.Info,
        };
    }

    private static string ReadMessage(JsonElement root)
    {
        if (root.TryGetProperty("@m", out JsonElement messageElement))
        {
            return messageElement.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("@mt", out JsonElement templateElement))
        {
            return templateElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ReadDetail(JsonElement root, string rawLine)
    {
        List<string> lines = new() { rawLine };
        if (root.TryGetProperty("@x", out JsonElement exceptionElement))
        {
            string? exception = exceptionElement.GetString();
            if (!string.IsNullOrWhiteSpace(exception))
            {
                lines.Add(exception);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
