using System.Xml.Linq;

using BTModMerger.Core.Interfaces;

using Microsoft.Extensions.Logging;

using static BTModMerger.Core.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Core;

public class Differ(
    ILogger<Differ> logger,
    BTMetadata metadata
)
    : IDiffer
{
    public XDocument Apply(XDocument @base, string basePath, XDocument mod, string modPath, bool alwaysOverride)
    {
        var baseRoot = @base.Root;
        var modRoot = mod.Root;

        if (baseRoot is null)
            throw new InvalidOperationException($"Attempt to process empty file: {basePath}");
        if (modRoot is null)
            throw new InvalidOperationException($"Attempt to process empty file: {modPath}");

        if (modRoot.IsBTOverride() && modRoot.Elements().Take(2).Count() == 1)
            modRoot = modRoot.Elements().First();

        if (baseRoot.Name != modRoot.Name && !modRoot.IsBTOverride() && baseRoot.Name != Elements.FusedBase)
            logger.LogWarning("Attempt to make a diff from incompatible mod-base pair. E.g. jobs.xml should be provided with vanilla jobs.xml as a base file.");

        var to = Diff();
        var toDocument = new XDocument(to);
        _ = ProcessRootElement(baseRoot, modRoot, to, alwaysOverride, new FileInfo(modPath).Name);
        return toDocument;
    }

    private void SewageDisposal(List<(XElement @base, XElement item, bool fromOverride, string path)> pile)
    {
        for (var i = 0; i < pile.Count; ++i)
        {
            var (@base, item, _, path) = pile[i];

            if (item.IsBTOverride())
            {
                pile.RemoveAt(i);
                pile.AddRange(item.Elements().Select(e => (@base, e, true, path)));
                --i;
            }
        }
    }

    private (XElement? toItem, string path) FindBaseItem(XElement element, XElement toContainer, XElement fromContainer, string root = "")
    {
        var childName = element.Name;
        var targets = toContainer.Elements(childName).ToArray();
        var fromItems = fromContainer.Elements(childName);

        var elementId = element.GetBTIdentifier(metadata);
        var elementIsIndexed = element.IsIndexed(metadata) || elementId is null && targets.Length > 1 || elementId is not null && targets.Count(e => e.GetBTIdentifier(metadata) == elementId) > 1;

        var elementIndex = -1;

        if (elementId is not null)
        {
            targets = targets.Where(t => t.GetBTIdentifier(metadata) == elementId).ToArray();
            fromItems = fromItems.Where(t => t.GetBTIdentifier(metadata) == elementId);
        }

        if (elementIsIndexed)
        {
            elementIndex = Array.IndexOf(fromItems.ToArray(), element);
            if (elementIndex > -1)
                targets = targets.Skip(elementIndex).Take(1).ToArray();
        }

        var toElement = targets.FirstOrDefault();
        var elementPath = FormPath(childName.ToString(), elementId, elementIndex, root);

        if (toElement == null && element == fromContainer || fromContainer.IsBTOverride() && fromContainer.Elements().Take(2).Count() == 1 && fromContainer.Elements().First() == element)
            toElement = toContainer;

        return (toElement, elementPath);
    }

    private static string FormPath(string childName, string? childId, int childIndex, string root = "")
    {
        var childPath = string.IsNullOrEmpty(root) ? childName.ToString() : $"{root}/{childName}";
        if (childId is not null) childPath += $"[@{childId}]";
        if (childIndex != -1) childPath += $"[{childIndex}]";
        return childPath;
    }

    private string MakeChildPath(XElement child, XElement baseContainer)
    {
        var childPath = child.Name.Fancify();
        var childId = child.GetBTIdentifier(metadata);
        var clones = baseContainer.Elements(child.Name).Where(e => e.GetBTIdentifier(metadata) == childId).ToArray();
        var childIndex = clones.Length > 1 ? Array.IndexOf(clones, child) : -1;
        if (childId is not null) childPath += $"[@{childId}]";
        if (childIndex != -1) childPath += $"[{childIndex}]";
        return childPath;
    }

    private bool ProcessRootElement(XElement baseRoot, XElement modRoot, XElement to, bool alwaysOverride, string modFilename)
    {
        var hasSomething = false;

        var pile = new List<(XElement @base, XElement item, bool fromOverride, string path)> { (baseRoot, modRoot, false, "") };

        // Removing root overrides
        SewageDisposal(pile);

        // Piling up items in actual roots with child overrides
        pile = pile
            .SelectMany(p =>
            {
                var @base = p.@base;

                if (@base.Name == Elements.FusedBase)
                {
                    var ownId = p.item.GetBTIdentifier(metadata);
                    var clones = @base.Elements(p.item.Name);
                    clones = clones.Where(e => e.Attribute(Attributes.File)?.Value is null || e.Attribute(Attributes.File)?.Value == modFilename);
                    if (ownId is not null) clones = clones.Where(e => e.GetBTIdentifier(metadata) == ownId);

                    var items = clones.Take(2).ToArray();

                    if (items.Length != 1)
                        throw new InvalidDataException($"Multiple elements of type {p.item.Name.Fancify()}[@{ownId}], consider changing id mapping or adding filenames.");

                    @base = items[0];
                }

                return p.item.HasAttributes
                    ? [(@base, p.item, p.fromOverride, CombineBTMMPaths(p.path, FormPath(p.item.Name.ToString(), p.item.GetBTIdentifier(metadata), -1)))]
                    : p.item.Elements()
                        .Select(e
                            => (@base, e, p.fromOverride, CombineBTMMPaths(p.path, FormPath(p.item.Name.ToString(), p.item.GetBTIdentifier(metadata), -1))));
            }
            ).ToList();

        // Removing child overrides
        SewageDisposal(pile);

        // now we have proper children that are not overrides and do not, I repeat, do not give us rectum cancer
        // Some files have a prefab as root entity. It is somewhat more convenient to handle them separately
        if (pile.Count == 1 && pile[0].item == (modRoot.IsBTOverride() ? modRoot.Elements().FirstOrDefault() : modRoot) && pile[0].item.HasAttributes)
        {
            modRoot = pile[0].item;
            var path = pile[0].path;
            var @base = pile[0].@base;

            hasSomething |= ProcessOverridden(@base, modRoot, to, modFilename, path);

            return hasSomething;
        }

        var justAdd = new Dictionary<string, List<XElement>>();

        foreach (var (@base, child, isOverride, path) in pile)
        {
            var (baseChild, childPath) = FindBaseItem(child, @base, modRoot, path);

            if (!isOverride && !alwaysOverride || baseChild is null)
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

            hasSomething |= ProcessOverridden(baseChild, child, to, modFilename, childPath);
        }

        foreach (var (path, list) in justAdd)
        {
            to.Add(AddElements(path, list));
            hasSomething = true;
        }

        if (alwaysOverride || baseRoot.Name == Elements.FusedBase)
            return hasSomething;

        var toRemove = new List<string>();

        foreach (var child in baseRoot.Elements())
        {
            var (modChild, _) = FindBaseItem(child, modRoot, baseRoot);

            // Processed in the first loop
            if (modChild is not null)
                continue;

            toRemove.Add(CombineBTMMPaths(baseRoot.Name.Fancify(), MakeChildPath(child, baseRoot)));
            hasSomething = true;
        }

        foreach (var path in toRemove)
            to.Add(RemoveElement(path));

        return hasSomething;
    }

    private bool ProcessOverridden(XElement @base, XElement mod, XElement to, string modFilename, string path)
    {
        var hasSomething = false;

        ProcessOverriddenAttributes(@base, mod, to, path, ref hasSomething);

        var toRemove = new List<XElement>();
        var tricky = new HashSet<XName>();

        foreach (var child in mod.Elements())
        {
            if (child.IsTricky(metadata))
            {
                tricky.Add(child.Name);
                continue;
            }

            var (baseChild, childPath) = FindBaseItem(child, @base, mod);

            if (baseChild is null)
            {
                to.Add(AddElements(path, 1, child));
                hasSomething = true;
            }
            else
                hasSomething |= ProcessOverridden(baseChild!, child, to, modFilename, CombineBTMMPaths(path, childPath));
        }

        foreach (var child in @base.Elements())
        {
            if (child.IsTricky(metadata))
            {
                tricky.Add(child.Name);
                continue;
            }

            var (modChild, _) = FindBaseItem(child, mod, @base);

            // Processed in the first loop
            if (modChild is not null)
                continue;

            toRemove.Add(RemoveElement(CombineBTMMPaths(path, MakeChildPath(child, @base))));
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
                    to.Add(AddElements(path, modCount, item));
                else
                    toRemove.Add(RemoveElement(path, -delta, item));

                hasSomething = true;
            }

            foreach (var (item, baseCount) in fromBaseDedup)
            {
                var (_, modCount) = fromModDedup.FirstOrDefault(p => XNode.DeepEquals(p.item, item));
                var delta = modCount - baseCount;

                if (modCount != 0)
                    continue;

                if (delta > 0)
                    to.Add(AddElements(path, modCount, item));
                else
                    toRemove.Add(RemoveElement(path, -delta, item));

                hasSomething = true;
            }
        }

        foreach (var item in toRemove)
            to.Add(item);

        return hasSomething;
    }

    private void ProcessOverriddenAttributes(XElement @base, XElement mod, XElement to, string path, ref bool hasSomething)
    {
        XElement? updateAttributes = null;

        XElement GetUpdateAttributes()
        {
            if (updateAttributes is not null)
                return updateAttributes;

            var clones = to.Elements(Elements.UpdateAttributes)
                .Where(e => e.GetBTMMPath() == path)
                .Take(2)
                .ToArray();

            if (clones.Length > 1)
                throw new Exception("Internal error");

            if (clones.Length > 0)
            {
                updateAttributes = clones[0];
                return updateAttributes;
            }

            updateAttributes = UpdateAttributes();
            updateAttributes.SetAttributeValue(Attributes.Path, path);
            to.Add(updateAttributes);
            return updateAttributes;
        }

        foreach (var attr in mod.Attributes())
        {
            var name = attr.Name;
            var value = attr.Value;
            var baseAttr = @base.GetBTAttributeCIS(name);

            if (value == baseAttr)
                continue;

            if (GetUpdateAttributes().GetBTAttributeCIS(AddNamespace + name.LocalName) is not null)
                throw new InvalidDataException($"Duplicate attribute {name.Fancify()} at {path}.");

            GetUpdateAttributes().Add(SetAttribute(name.LocalName, value));
            hasSomething = true;
        }

        foreach (var attr in @base.Attributes())
        {
            var name = attr.Name;

            if (attr.Name == Attributes.File)
                continue;

            var modAttr = mod.GetBTAttributeCIS(name);

            // Processed in the first loop
            if (modAttr is not null)
                continue;

            GetUpdateAttributes().SetAttributeValue(RemoveNamespace + name.LocalName, "");
            hasSomething = true;
        }
    }
}
