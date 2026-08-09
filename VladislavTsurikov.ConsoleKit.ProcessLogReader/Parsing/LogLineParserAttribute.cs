namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LogLineParserAttribute : Attribute
{
    public LogLineParserAttribute(int order)
    {
        Order = order;
    }

    public int Order { get; }
}
