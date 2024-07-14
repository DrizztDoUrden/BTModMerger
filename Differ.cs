using System.Xml.Linq;

namespace BTModMerger;

internal static class Differ
{
    public static void Apply(string basePath, string modPath, string outputPath)
    {
        var (@base, mod) = Load(basePath, modPath);
        var output = Process(@base, basePath, mod, modPath);

        var containingDir = new FileInfo(outputPath).Directory;
        if (!containingDir!.Exists)
            containingDir.Create();

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

        if (baseRoot.Name != modRoot.Name && !modRoot.IsBTOverride())
            Console.Error.WriteLine("[Warning] Attempt to make a diff from incompatible mod-base pair. E.g. jobs.xml should be provided with vanilla jobs.xml as a base file.");

        _ = ProcessRootElement(baseRoot, modRoot, out var tmp);
        var output = new XDocument(
            new XElement(BTMMSchema.Elements.Diff,
                new XAttribute(XNamespace.Xmlns + BTMMSchema.NamespaceAlias, BTMMSchema.Namespace),
                new XAttribute(XNamespace.Xmlns + BTMMSchema.AddNamespaceAlias, BTMMSchema.AddNamespace),
                new XAttribute(XNamespace.Xmlns + BTMMSchema.RemoveNamespaceAlias, BTMMSchema.RemoveNamespace),
                tmp
            )
        );
        return output;
    }

    private static void SewageDisposal(List<(XElement item, bool fromOverride, string path)> pile)
    {
        for (var i = 0; i < pile.Count; ++i)
        {
            var (item, _, path) = pile[i];

            if (item.IsBTOverride())
            {
                pile.RemoveAt(i);
                pile.AddRange(item.Elements().Select(e => (e, true, path)));
                --i;
            }
        }
    }

    private static (XElement? toItem, string path) FindBaseItem(XElement element, XElement toContainer, XElement fromContainer, string path = "")
    {
        var childName = element.Name;
        var targets = toContainer.Elements(childName).ToArray();

        var childId = element.GetBTIdentifier();
        var childIsIndexed = element.IsIndexed() || childId is null && targets.Length > 1 || childId is not null && targets.Count(e => e.GetBTIdentifier() == childId) > 1;

        var childIndex = -1;

        if (childIsIndexed)
        {
            childIndex = fromContainer.Elements(childName).ToList().IndexOf(element);
            targets = targets.Skip(childIndex).Take(1).ToArray();
        }
        else
        {
            if (childId is not null)
                targets = targets.Where(t => t.GetBTIdentifier() == childId).ToArray();
        }

        var toChild = targets.FirstOrDefault();

        var childPath = string.IsNullOrEmpty(path) ? childName.ToString() : $"{path}/{childName}";
        if (childId is not null) childPath += $"[@{childId}]";
        if (childIsIndexed) childPath += $"[{childIndex}]";

        return (toChild, childPath);
    }

    private static bool ProcessRootElement(XElement baseRoot, XElement modRoot, out List<XObject> results)
    {
        results = [];
        var hasSomething = false;
        var pile = new List<(XElement item, bool fromOverride, string path)> { (modRoot, false, "") };

        // Removing root overrides
        SewageDisposal(pile);
        // Piling up items in actual roots with child overrides
        pile = pile.SelectMany(p => p.item.Elements().Select(e => (e, p.fromOverride, p.item.Name.ToString()))).ToList();
        // Removing child overrides
        SewageDisposal(pile);
        // now we have proper children that are not overrides and do not, I repeat, do not give us rectum cancer

        var justAdd = new Dictionary<string, List<XElement>>();

        foreach (var (child, isOverride, path) in pile)
        {
            if (!isOverride)
            {
                if (!justAdd.TryGetValue(path, out var list))
                {
                    list = [];
                    justAdd.Add(path, list);
                }
                list.Add(child);
                hasSomething = true;
                continue;
            }

            var (baseChild, childPath) = FindBaseItem(child, baseRoot, modRoot, path);

            if (baseChild is not null)
            {
                if (ProcessOverridden(baseChild, child, out var tmp))
                {
                    results.Add(BTMMSchema.Into(childPath, tmp));
                    hasSomething = true;
                }
            }
            else
            {
                // Rectum cancer of overriding non-existant elements
                if (!justAdd.TryGetValue(path, out var list))
                {
                    list = [];
                    justAdd.Add(path, list);
                }
                list.Add(child);
            }
        }

        foreach (var (path, list) in justAdd)
        {
            results.Add(BTMMSchema.Into(path, list));
            hasSomething = true;
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
            if (child.IsTricky())
            {
                tricky.Add(child.Name);
                continue;
            }

            var (baseChild, childPath) = FindBaseItem(child, @base, mod);

            if (baseChild is null)
            {
                results.AddRange(BTMMSchema.AddElements(child));
                hasSomething = true;
            }
            else
            {
                if (ProcessOverridden(baseChild!, child, out var tmp))
                {
                    results.Add(BTMMSchema.Into(childPath, tmp));
                    hasSomething = true;
                }
            }
        }

        foreach (var child in @base.Elements())
        {
            if (child.IsTricky())
            {
                tricky.Add(child.Name);
                continue;
            }

            var (modChild, _) = FindBaseItem(child, mod, @base);

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
