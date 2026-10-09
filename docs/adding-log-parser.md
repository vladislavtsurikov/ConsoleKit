# Adding a log line parser

Implement `ILogLineParser`, expose a parameterless constructor and mark the concrete
class with `LogLineParserAttribute`. Lower order values run first. The built-in plain text
parser is the terminal fallback and has order `1000`.

```csharp
[LogLineParser(order: 20)]
public sealed class VendorLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLogLine)
    {
        parsedLogLine = null;
        return false;
    }
}
```

`LogLineParserRegistry` discovers parsers once and caches the ordered result. Adding a
parser must not require a manual registry change. Add a unit test for every parser's
recognized lines and for its fallback behavior.
