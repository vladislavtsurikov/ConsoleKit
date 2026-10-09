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
            LogEntryLevel level = ReadLevel(root);
            string message = ReadMessage(root);
            string detail = ReadDetail(root, line);
            parsedLine = new ParsedLogLine(timestamp, LogLevels.ToSeverity(level), message, detail)
            {
                Level = level,
                Category = ReadString(root, "SourceContext"),
                Properties = ReadProperties(root),
            };
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

    private static LogEntryLevel ReadLevel(JsonElement root)
    {
        // Compact JSON omits "@l" for Information.
        string? level = ReadString(root, "@l");
        return LogLevels.TryParse(level, out LogEntryLevel parsed) ? parsed : LogEntryLevel.Information;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    /// <summary>The user properties of the event: everything that is not an "@" field.</summary>
    private static IReadOnlyDictionary<string, string>? ReadProperties(JsonElement root)
    {
        Dictionary<string, string>? properties = null;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name.StartsWith('@'))
            {
                continue;
            }

            properties ??= new Dictionary<string, string>(StringComparer.Ordinal);
            properties[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return properties;
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
