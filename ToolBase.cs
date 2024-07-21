using System.Xml;
using System.Xml.Linq;

using static BTModMerger.BTMMSchema;

namespace BTModMerger;

static internal class ToolBase
{
    public static void SaveResult(string? outputPath, XDocument? result)
    {
        if (result == null || !result.Elements().Any())
            return;

        if (!string.IsNullOrEmpty(outputPath))
        {
            var containingDir = new FileInfo(outputPath).Directory;
            if (!containingDir!.Exists)
                containingDir.Create();
        }

        var outputFile = string.IsNullOrWhiteSpace(outputPath)
            ? Console.OpenStandardOutput()
            : File.Create(outputPath);

        using (var writer = XmlWriter.Create(outputFile, WriterSettings))
            result.Save(writer);

        if (outputFile is FileStream)
            outputFile.Dispose();
    }

    public static class Log
    {
        public static void Info(string message)
        {
            var before = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Error.Write("[Info] ");
            Console.ForegroundColor = before;
            Console.Error.WriteLine(message);
        }

        public static void Warning(string message)
        {
            var before = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.Write("[Warning] ");
            Console.ForegroundColor = before;
            Console.Error.WriteLine(message);
        }

        public static void Error(string message)
        {
            var before = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write("[Error] ");
            Console.ForegroundColor = before;
            Console.Error.WriteLine(message);
        }
    }

    public static (int start, int end) FindSubscript(string from, string diffPath)
    {
        var start = from.IndexOf('[');
        if (start < 0) return (-1, -1);
        var end = from.IndexOfAny(['[', ']'], start + 1);

        if (end < 0) throw new InvalidDataException("Missing ']' character in a path subscript operator.");

        var level = from[end] == '[' ? 2 : 0;
        while (level > 0)
        {
            end = from.IndexOfAny(['[', ']'], end + 1);
            level += from[end] == '[' ? 1 : -1;
        }

        if (start + 1 == end)
            throw new InvalidDataException("Empty subscript operator.");

        return (start, end);
    }

    public static string? ExtractSubscript(ref string from, string diffPath)
    {
        var (start, end) = FindSubscript(from, diffPath);
        if (start == -1)
            return null;
        var value = from[(start + 1)..(end)];
        from = from.Remove(start, end - start + 1);
        return value;
    }

    public static (string? id, int idx) ParseSubscript(string? subscript)
    {
        if (subscript is null)
            return (null, -1);

        if (subscript[0] == '@')
        {
            subscript = subscript[1..];
            return (subscript, -1);
        }

        return (null, int.Parse(subscript));
    }

    public static IEnumerable<XElement> FilterBySubscript(this IEnumerable<XElement> elements, string? subscript, string diffPath)
    {
        var (id, idx) = ParseSubscript(subscript);

        if (id is not null)
            return elements.Where(e => e.GetBTIdentifier() == id);
        if (idx != -1)
            return elements.Skip(idx).Take(1);
        return elements;
    }

    public static (string? ss0, string? ss1) ExtractSubscripts(ref string from, string diffPath)
    {
        return (
            ExtractSubscript(ref from, diffPath),
            ExtractSubscript(ref from, diffPath)
        );
    }

    public static IEnumerable<XElement> FilterBySubscripts(this IEnumerable<XElement> elements, (string? ss0, string? ss1) subscripts, string diffPath)
    {
        elements = FilterBySubscript(elements, subscripts.ss0, diffPath);
        elements = FilterBySubscript(elements, subscripts.ss1, diffPath);
        return elements;
    }

    public static string[] SplitPath(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Fancify(this XName name)
    {
        if (name.Namespace == Namespace)
            return $"{NamespaceAlias}:{name.LocalName}";
        if (name.Namespace == AddNamespace)
            return $"{AddNamespaceAlias}:{name.LocalName}";
        if (name.Namespace == RemoveNamespace)
            return $"{RemoveNamespaceAlias}:{name.LocalName}";
        return name.ToString();
    }

    public static string CombineBTMMPaths(params string[] paths)
        => string.Join('/', paths.Where(p => !string.IsNullOrEmpty(p)));

    public static string CombineBTMMPaths(string path, XName newPart)
        => CombineBTMMPaths(path, newPart.Fancify());
}
