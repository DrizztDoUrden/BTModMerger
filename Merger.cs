using System.Xml.Linq;

namespace BTModMerger;

sealed internal class Merger
{
    public static void Apply(Stream baseFile, string modPath, Stream outputFile)
    {
        var @base = XDocument.Load(baseFile, LoadOptions.None);
        var mod = LoadMod(modPath);

        if (mod.Root!.Name != BTMMSchema.Elements.Diff)
            throw new InvalidDataException($"Mod ({modPath}) should be in diff format.");

        Apply(mod.Root!, @base, "/Diff");
        @base.Save(outputFile);
    }

    private static XDocument LoadMod(string modPath)
    {
        using var modFile = File.OpenRead(modPath);
        return XDocument.Load(modFile, LoadOptions.None);
    }

    private static void Apply(XElement diffElement, XContainer to, string diffPath)
    {
        foreach (var child in diffElement.Elements(BTMMSchema.Elements.RemoveAttribute))
        {
            var path = child.GetBTMMPath();
            ((XElement)to).Attribute(path!)!.Remove();
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.SetAttribute))
        {
            var path = child.GetBTMMPath();
            var value = child.GetBTMMValue();
            ((XElement)to).SetAttributeValue(path!, value!);
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.Into))
        {
            var target = child.GetBTMMPath()!;
            var childDiffPath = $"{diffPath}/{target}";
            var targetElement = GetTarget(target, to, diffPath);

            Apply(child, targetElement, childDiffPath);
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.RemoveElements).Reverse())
        {
            var target = child.GetBTMMPath()!;

            if (target is not null)
            {
                var targetElement = GetTarget(target, to, diffPath);
                targetElement.Remove();
            }
            else
            {
                var children = to.Elements().ToArray();
                var normalizedChildren = children.Select(e => XElementComparator.NormalizeElement(e)).ToArray();

                var targets = child.Elements()
                    .Select(e => XElementComparator.NormalizeElement(e))
                    .Select(e => Array.FindIndex(normalizedChildren, e2 => XNode.DeepEquals(e, e2)))
                    .Select(idx => children[idx]);

                foreach (var toDelete in targets)
                    toDelete.Remove();
            }
        }

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.AddElements))
        {
            var amount = child.GetBTMMAmount()!;

            for (var i = 0; i < amount; ++i)
                foreach (var toAdd in child.Elements())
                    to.Add(toAdd);
        }
    }

    private static (int start, int end) FindSubscript(string from, string diffPath)
    {
        var start = from.IndexOf('[');
        if (start < 0) return (-1, -1);
        var end = from.IndexOf(']');
        if (end < 0) throw new InvalidDataException("Missing ']' character in a path subscript operator.");
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

    private static XElement GetTarget(string target, XContainer from, string diffPath)
    {
        var ss0 = ExtractSubscript(ref target, diffPath);
        var ss1 = ExtractSubscript(ref target, diffPath);

        var elems = from.Elements(target);
        elems = FilterBySubscript(elems, ss0, diffPath);
        elems = FilterBySubscript(elems, ss1, diffPath);
        var array = elems.ToArray();

        if (array.Length != 1)
            throw new InvalidDataException($"A subscript has produced zero or more than one result at {diffPath}");

        return array[0];
    }
}
