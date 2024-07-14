using System.Collections.Generic;
using System.Xml.Linq;

namespace BTModMerger;

internal static class Differ
{
    public static void Apply(string basePath, string modPath, string outputPath)
    {
        var (@base, mod) = Load(basePath, modPath);
        var output = Process(@base, basePath, mod, modPath);
        using var outputFile = File.Create(outputPath);
        output.Save(outputFile, SaveOptions.None);
    }

    private static (XDocument @base, XDocument mod) Load(string basePath, string modPath)
    {
        using var baseFile = File.OpenRead(basePath);
        using var modFile = File.OpenRead(modPath);

        var @base = XDocument.Load(baseFile, LoadOptions.None);
        var mod = XDocument.Load(modFile, LoadOptions.None);

        return (@base, mod);
    }

    private static XDocument Process(XDocument @base, string basePath, XDocument mod, string modPath)
    {
        var baseRoot = @base.Root;
        var modRoot = mod.Root;

        if (baseRoot is null)
            throw new InvalidOperationException($"Attempt to process empty file: {basePath}");
        if (modRoot is null)
            throw new InvalidOperationException($"Attempt to process empty file: {modPath}");

        if (baseRoot.Name != modRoot.Name)
            throw new InvalidOperationException("Attempt to make a diff from incompatible mod-base pair. E.g. jobs.xml should be provided with vanilla jobs.xml as a base file.");

        var output = new XDocument(
            new XElement(BTMMSchema.Elements.Diff,
                new XAttribute(XNamespace.Xmlns + BTMMSchema.NamespaceAlias, BTMMSchema.Namespace),
                new XAttribute(XNamespace.Xmlns + BTMMSchema.AddNamespaceAlias, BTMMSchema.AddNamespace),
                new XAttribute(XNamespace.Xmlns + BTMMSchema.RemoveNamespaceAlias, BTMMSchema.RemoveNamespace),
                BTMMSchema.Into(modRoot.Name.ToString(), ProcessRootElements(baseRoot, modRoot))
            )
        );
        return output;
    }

    private static List<XObject> ProcessRootElements(XElement baseRoot, XElement modRoot)
    {
        var results = new List<XObject>();
        var toAdd = new List<XElement>();

        foreach (var child in modRoot.Elements())
        {
            if (child.Name == "Override")
            {
                if (ProcessOverride(baseRoot, child, out var tmp))
                {
                    results.AddRange(tmp);
                }
                continue;
            }

            var id = child.GetBTIdentifier();
            if (id is null)
            {
                // This is some element group
                results.Add(BTMMSchema.Into(child.Name.ToString(), ProcessRootElements(baseRoot.Element(child.Name) ?? baseRoot, child)));
                continue;
            }

            toAdd.Add(child);
        }

        if (toAdd.Count > 0)
            results.AddRange(BTMMSchema.AddElements(toAdd));

        return results;
    }

    private static bool ProcessOverride(XElement @base, XElement @override, out List<XObject> results)
    {
        var hasSomething = false;
        results = [];

        foreach (var child in @override.Elements())
        {
            // For some reason BT generally allows nested overrides with no special meaning
            if (child.Name == "Override")
            {
                if (ProcessOverride(@base, child, out var tmp))
                {
                    results.AddRange(tmp);
                    hasSomething = true;
                }
                continue;
            }

            var id = child.GetBTIdentifier();
            var childPath = id is null
                ? child.Name.ToString()
                : $"{child.Name}[@{id}]";

            {
                if (ProcessOverridden(@base.Elements().First(e => e.GetBTIdentifier() == id), child, out var tmp))
                {
                    results.Add(BTMMSchema.Into(childPath, tmp));
                    hasSomething = true;
                }
            }
        }

        return hasSomething;
    }

    private static bool ProcessOverridden(XElement @base, XElement mod, out List<XObject> results)
    {
        var hasSomething = false;
        results = [];

        ProcessOverriddenAttributes(@base, mod, ref results, ref hasSomething);

        var toAdd = new List<XElement>();
        var toRemove = new List<XElement>();
        var tricky = new HashSet<XName>();

        foreach (var child in mod.Elements())
        {
            var isTricky = BTMetadata.Instance.Tricky.Contains(child.Name.LocalName);

            if (isTricky)
            {
                tricky.Add(child.Name);
                continue;
            }

            var name = child.Name;
            var id = child.GetBTIdentifier();
            var isIndexed = BTMetadata.Instance.Indexed.Contains(child.Name.LocalName);

            var index = isIndexed
                ? mod.Elements(name).ToList().IndexOf(child)
                : 0;

            var baseChild = isIndexed
                ? @base.Elements(name).Skip(index).FirstOrDefault()
                : @base.Elements(name).FirstOrDefault(e => e.GetBTIdentifier() == id);

            if (baseChild is null)
            {
                toAdd.Add(child);
                hasSomething = true;
                continue;
            }

            var childPath = name.ToString();
            if (id is not null) childPath += $"[@{id}]";
            if (isIndexed) childPath += $"[{index}]";

            if (ProcessOverridden(baseChild, child, out var tmp))
            {
                results.Add(BTMMSchema.Into(childPath, tmp));
                hasSomething = true;
            }
        }

        foreach (var child in @base.Elements())
        {
            var isTricky = BTMetadata.Instance.Tricky.Contains(child.Name.LocalName);

            if (isTricky)
            {
                tricky.Add(child.Name);
                continue;
            }

            var name = child.Name;
            var id = child.GetBTIdentifier();
            var isIndexed = BTMetadata.Instance.Indexed.Contains(child.Name.LocalName);

            var index = isIndexed
                ? @base.Elements(name).ToList().IndexOf(child)
                : 0;

            var modChild = isIndexed
                ? mod.Elements(name).Skip(index).FirstOrDefault()
                : mod.Elements(name).FirstOrDefault(e => e.GetBTIdentifier() == id);

            // Processed in the first loop
            if (modChild is not null)
                continue;

            toRemove.AddRange(BTMMSchema.RemoveElements(child));
            hasSomething = true;
        }

        foreach (var name in tricky)
        {
            var fromModDedup = mod.Elements(name).Select(e => XElementComparator.NormalizeElement(e)).Deduplicate();
            var fromBase = @base.Elements(name).Select(e => XElementComparator.NormalizeElement(e)).ToList();
            var fromBaseDedup = fromBase.Deduplicate();

            foreach (var (item, modCount) in fromModDedup)
            {
                var (_, baseCount) = fromBaseDedup.FirstOrDefault(p => XNode.DeepEquals(p.item, item));
                var delta = modCount - baseCount;

                if (delta == 0)
                    continue;

                if (delta > 0)
                {
                    if (delta == 1)
                        toAdd.Add(item);
                    else
                        results.Add(BTMMSchema.AddElements(modCount, item));
                }
                else
                {
                    toRemove.Add(BTMMSchema.RemoveElement(-delta, item));
                }

                hasSomething = true;
            }


            foreach (var (item, baseCount) in fromBaseDedup)
            {
                var (_, modCount) = fromModDedup.FirstOrDefault(p => XNode.DeepEquals(p.item, item));
                var delta = modCount - baseCount;

                if (modCount != 0)
                    continue;

                if (delta > 0)
                {
                    if (delta == 1)
                        toAdd.Add(item);
                    else
                        results.Add(BTMMSchema.AddElements(modCount, item));
                }
                else
                {
                    toRemove.Add(BTMMSchema.RemoveElement(-delta, item));
                }

                hasSomething = true;
            }
        }

        if (toAdd.Count > 0)
            results.AddRange(BTMMSchema.AddElements(toAdd));

        if (toRemove.Count > 0)
            results.AddRange(toRemove);

        return hasSomething;
    }

    private static void ProcessOverriddenAttributes(XElement @base, XElement mod, ref List<XObject> results, ref bool hasSomething)
    {
        foreach (var attr in mod.Attributes())
        {
            var name = attr.Name;
            var value = attr.Value;
            var baseAttr = @base.Attributes().FirstOrDefault(attr => attr.Name == name);

            if (value == baseAttr?.Value)
                continue;

            results.Add(BTMMSchema.SetAttribute(name.LocalName, value));
            hasSomething = true;
        }

        foreach (var attr in @base.Attributes())
        {
            var name = attr.Name;
            var modAttr = mod.Attributes().FirstOrDefault(attr => attr.Name == name);

            // Processed in the first loop
            if (modAttr is not null)
                continue;

            results.Add(BTMMSchema.RemoveAttribute(name.LocalName));
            hasSomething = true;
        }

    }
}
