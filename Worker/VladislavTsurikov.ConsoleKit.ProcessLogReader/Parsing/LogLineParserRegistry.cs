using System.Reflection;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

public static class LogLineParserRegistry
{
    private static readonly Lazy<IReadOnlyList<ILogLineParser>> s_parsers = new(ScanParsers);
    private static int s_scanCount;

    public static IReadOnlyList<ILogLineParser> Parsers => s_parsers.Value;

    public static int ScanCount => Volatile.Read(ref s_scanCount);

    private static IReadOnlyList<ILogLineParser> ScanParsers()
    {
        Interlocked.Increment(ref s_scanCount);
        List<(int Order, ILogLineParser Parser)> parsers = new();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                LogLineParserAttribute? attribute = type.GetCustomAttribute<LogLineParserAttribute>();
                if (attribute is null ||
                    type.IsAbstract ||
                    !typeof(ILogLineParser).IsAssignableFrom(type))
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is ILogLineParser parser)
                {
                    parsers.Add((attribute.Order, parser));
                }
            }
        }

        return parsers
            .OrderBy(item => item.Order)
            .Select(item => item.Parser)
            .ToArray();
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .Where(type => type is not null)
                .Cast<Type>()
                .ToArray();
        }
    }
}
