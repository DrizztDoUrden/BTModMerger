using System;
using System.IO;
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
            var targetElement = GetTarget(target, to, diffPath);

            foreach (var attr in child.Attributes())
            {
                var operation = attr.Name.Namespace;
                var attrTarget = attr.Name.LocalName;
                if (operation == BTMMSchema.AddNamespace) { targetElement.SetAttributeValue(attrTarget, attr.Value); }
                else if (operation == BTMMSchema.RemoveNamespace) { targetElement.Attribute(attrTarget)!.Remove(); }
                else if (operation == BTMMSchema.Namespace) { /* target path, etc */ }
                else throw new InvalidDataException($"Invalid attribute change operation (xmlns): {operation} at {childDiffPath}/{attr.Name}");
            }

            Apply(child, targetElement, childDiffPath);
        }

        var toRemove = new List<(XElement item, int amount)>();
        var originalToChildren = to.Elements().ToArray();
        var normalizedChildren = originalToChildren.Select(e => XElementComparator.NormalizeElement(e)).ToArray();

        foreach (var child in diffElement.Elements(BTMMSchema.Elements.RemoveElements))
        {
            var target = child.GetBTMMPath()!;

            if (target is not null)
            {
                var targetItem = GetTarget(target, to, diffPath);
                toRemove.Add((targetItem, child.GetBTMMAmount()));
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
            } else if (child.Name.Namespace == BTMMSchema.RemoveNamespace)
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
