using System.Globalization;
using System.Text.Json;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Files;

/// <summary>The header line of a session file.</summary>
public sealed record LogSessionHeader(
    string Product,
    string App,
    string? Version,
    int ProcessId,
    DateTimeOffset Started,
    string Machine,
    int Format = LogJsonFormat.FormatVersion);

/// <summary>
/// One JSON object per line (see docs/log-files.md). Short keys keep files small; the first line is
/// the session header.
/// </summary>
public static class LogJsonFormat
{
    public const int FormatVersion = 1;

    private static readonly JsonWriterOptions s_writerOptions = new() { Indented = false };

    public static string WriteHeader(LogSessionHeader header)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, s_writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("session");
            writer.WriteString("product", header.Product);
            writer.WriteString("app", header.App);
            if (header.Version is not null)
            {
                writer.WriteString("version", header.Version);
            }

            writer.WriteNumber("pid", header.ProcessId);
            writer.WriteString("started", header.Started);
            writer.WriteString("machine", header.Machine);
            writer.WriteNumber("format", header.Format);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string WriteEntry(LogEntry entry)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, s_writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("t", entry.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("l", entry.Level.ToString());
            writer.WriteString("src", entry.SourceId);
            if (entry.ProcessId is { } processId)
            {
                writer.WriteNumber("pid", processId);
            }

            if (entry.Stream != LogStream.None)
            {
                writer.WriteString("stream", entry.Stream == LogStream.StandardError ? "stderr" : "stdout");
            }

            if (!string.IsNullOrEmpty(entry.Category))
            {
                writer.WriteString("cat", entry.Category);
            }

            writer.WriteString("msg", entry.Message);
            if (!string.IsNullOrEmpty(entry.Detail) && !string.Equals(entry.Detail, entry.Message, StringComparison.Ordinal))
            {
                writer.WriteString("detail", entry.Detail);
            }

            if (entry.Properties is { Count: > 0 } properties)
            {
                writer.WriteStartObject("props");
                foreach (KeyValuePair<string, string> property in properties)
                {
                    writer.WriteString(property.Key, property.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryReadHeader(string line, out LogSessionHeader? header)
    {
        header = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("session", out JsonElement session))
            {
                return false;
            }

            header = new LogSessionHeader(
                String(session, "product") ?? string.Empty,
                String(session, "app") ?? string.Empty,
                String(session, "version"),
                session.TryGetProperty("pid", out JsonElement pid) ? pid.GetInt32() : 0,
                session.TryGetProperty("started", out JsonElement started) ? started.GetDateTimeOffset() : default,
                String(session, "machine") ?? string.Empty,
                session.TryGetProperty("format", out JsonElement format) ? format.GetInt32() : FormatVersion);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool TryReadEntry(string line, out LogEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("session", out _))
            {
                return false;
            }

            LogEntryLevel level = LogLevels.TryParse(String(root, "l"), out LogEntryLevel parsed) ? parsed : LogEntryLevel.Information;
            string message = String(root, "msg") ?? string.Empty;
            Dictionary<string, string>? properties = null;
            if (root.TryGetProperty("props", out JsonElement props) && props.ValueKind == JsonValueKind.Object)
            {
                properties = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (JsonProperty property in props.EnumerateObject())
                {
                    properties[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            entry = new LogEntry(
                0,
                DateTimeOffset.Parse(String(root, "t") ?? string.Empty, CultureInfo.InvariantCulture),
                LogLevels.ToSeverity(level),
                String(root, "src") ?? string.Empty,
                message,
                String(root, "detail") ?? message)
            {
                Level = level,
                ProcessId = root.TryGetProperty("pid", out JsonElement pid) ? pid.GetInt32() : null,
                Stream = String(root, "stream") switch
                {
                    "stderr" => LogStream.StandardError,
                    "stdout" => LogStream.StandardOutput,
                    _ => LogStream.None,
                },
                Category = String(root, "cat"),
                Properties = properties,
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? String(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
