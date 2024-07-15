using System.Xml;
using System.Xml.Linq;

namespace BTModMerger;

sealed internal class Aplyier
{
    public static void Apply(string? basePath, string modPath, string? outputPath, bool asOverride)
    {
        var baseFile = string.IsNullOrWhiteSpace(basePath)
            ? Console.OpenStandardInput()
            : File.OpenRead(basePath);

        var @base = XDocument.Load(baseFile, LoadOptions.None);

        if (baseFile is FileStream)
            baseFile.Dispose();

        var mod = LoadMod(modPath);

        if (mod.Root!.Name != BTMMSchema.Elements.Diff)
            throw new InvalidDataException($"Mod ({modPath}) should be in diff format.");

        var to = asOverride && !@base.Root!.HasAttributes ? new XDocument() : @base;
        Apply(mod.Root!, @base, to, "/Diff");

        if (!to.Elements().Any())
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

        using (var writer = XmlWriter.Create(outputFile, BTMMSchema.WriterSettings))
            to.Save(writer);

        if (outputFile is FileStream)
            outputFile.Dispose();
    }

    private static XDocument LoadMod(string modPath)
    {
        using var modFile = File.OpenRead(modPath);
        return XDocument.Load(modFile, LoadOptions.None);
    }

    private static void Apply(XElement diffElement, XContainer from, XContainer to, string diffPath)
    {
        foreach (var child in diffElement.Elements(BTMMSchema.Elements.SetAttribute))
        {
            var path = child.GetBTMMPath();
            var value = child.GetBTMMValue();
            ((XElement)to).SetAttributeValue(path!, value!);
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.Into))
        {
            var childDiffPath = $"{diffPath}/{child.Name}";
            var target = child.GetBTMMPath()!;
            var (fromTarget, toTarget) = GetTarget(target, from, to, diffPath);

            foreach (var attr in child.Attributes())
            {
                var operation = attr.Name.Namespace;
                var attrTarget = attr.Name.LocalName;
                if (operation == BTMMSchema.AddNamespace) { toTarget.SetAttributeValue(attrTarget, attr.Value); }
                else if (operation == BTMMSchema.RemoveNamespace) { toTarget.Attribute(attrTarget)!.Remove(); }
                else if (operation == BTMMSchema.Namespace) { /* target path, etc */ }
                else throw new InvalidDataException($"Invalid attribute change operation (xmlns): {operation} at {childDiffPath}/{attr.Name}");
            }

            Apply(child, fromTarget, toTarget, childDiffPath);
        }

        var toRemove = new List<(XElement item, int amount)>();
        var originalToChildren = to.Elements().ToArray();
        var normalizedChildren = originalToChildren.Select(e => XElementComparator.NormalizeElement(e)).ToArray();

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.RemoveElements))
        {
            var target = child.GetBTMMPath()!;

            if (target is not null)
            {
                var (fromTarget, toTarget) = GetTarget(target, from, to, diffPath);
                toRemove.Add((toTarget, child.GetBTMMAmount()));
            }
            else
            {
                toRemove.AddRange(child.Elements().Select(item =>
                {
                    var tri = new XElement(item);
                    tri.Attribute(BTMMSchema.Attributes.Amount)?.Remove();
                    return (tri, item.GetBTMMAmount());
                }));
            }
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.AddElements))
        {
            var amount = child.GetBTMMAmount()!;

            for (var i = 0; i < amount; ++i)
                foreach (var toAdd in child.Elements())
                    to.Add(toAdd);
        }

        foreach (var child in diffElement.Elements())
        {
            if (child.Name.Namespace == BTMMSchema.AddNamespace || child.Name.Namespace == XNamespace.None)
            {
                var toAdd = new XElement(child);
                toAdd.Attribute(BTMMSchema.Attributes.Amount)?.Remove();
                var amount = child.GetBTMMAmount()!;

                for (var i = 0; i < amount; ++i)
                    to.Add(toAdd);
            }
            else if (child.Name.Namespace == BTMMSchema.RemoveNamespace)
            {
                var item = new XElement(child)
                {
                    Name = child.Name.LocalName,
                };
                item.Attribute(BTMMSchema.Attributes.Amount)?.Remove();

                toRemove.Add((item, child.GetBTMMAmount()));
            }
        }

        toRemove = toRemove
            .Select(pair => (XElementComparator.NormalizeElement(pair.item), pair.amount))
            .Deduplicate();

        foreach (var (item, amount) in toRemove)
        {
            var idx = Array.FindIndex(normalizedChildren, e2 => XNode.DeepEquals(item, e2));

            originalToChildren[idx].Remove();
        }
    }

    private static (int start, int end) FindSubscript(string from, string diffPath)
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

        return (start, end);
    }

    private static string? ExtractSubscript(ref string from, string diffPath)
    {
        var (start, end) = FindSubscript(from, diffPath);
        if (start == -1)
            return null;
        var value = from[(start + 1)..(end)];
        from = from.Remove(start, end - start + 1);
        return value;
    }

    private static IEnumerable<XElement> FilterBySubscript(IEnumerable<XElement> elements, string? subscript, string diffPath)
    {
        if (subscript is null)
            return elements;

        if (subscript.Length == 0)
            throw new InvalidDataException("Empty subscript operator.");

        if (subscript[0] == '@')
        {
            subscript = subscript[1..];
            return elements.Where(e => e.GetBTIdentifier() == subscript);
        }

        var idx = int.Parse(subscript);
        return elements.Skip(idx).Take(1);
    }

    private static (XContainer from, XElement to) GetTarget(string target, XContainer from, XContainer to, string diffPath)
    {
        var parts = target.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        XElement? targetElement = null;

        if (to is XDocument)
            to = to.Element(BTMMSchema.Elements.Override) ?? to;

        foreach (var part in parts)
        {
            var partCopy = part;

            var ss0 = ExtractSubscript(ref partCopy, diffPath);
            var ss1 = ExtractSubscript(ref partCopy, diffPath);

            var fromElems = from.Elements(partCopy);
            fromElems = FilterBySubscript(fromElems, ss0, diffPath);
            fromElems = FilterBySubscript(fromElems, ss1, diffPath);
            var fromArray = fromElems.Take(2).ToArray();

            if (fromArray.Length != 1)
                throw new InvalidDataException($"A subscript has produced zero or more than one result at {diffPath}");

            var fromItem = fromArray[0];

            if (from != to)
            {
                var toElems = new[]
                    {
                        to.Elements(partCopy),
                        to.Elements(BTMMSchema.Elements.Override).SelectMany(e => e.Elements(partCopy)),
                    }
                    .SelectMany(e => e);

                toElems = FilterBySubscript(toElems, ss0, diffPath);
                toElems = FilterBySubscript(toElems, ss1, diffPath);
                var toArray = toElems.Take(2).ToArray();

                if (toArray.Length == 1)
                {
                    targetElement = toArray[0];
                    to = targetElement;
                }
                else
                {
                    // Means we are in override mode
                    if (to is XDocument)
                    {
                        targetElement = new(fromItem.Name);
                        to.Add(new XElement(BTMMSchema.Elements.Override, targetElement));
                        to = targetElement;
                    }
                    else
                    {
                        targetElement = new(fromItem);
                        to.Add(targetElement);
                        to = targetElement;
                        fromItem = targetElement;
                    }
                }
            }
            else
            {
                targetElement = fromItem;
                to = fromItem;
            }

            from = fromItem;
        }

        if (targetElement is null)
            throw new InvalidDataException("Empty paths are not supported");

        return (from, targetElement);
    }
}
