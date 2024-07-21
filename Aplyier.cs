using System.Xml.Linq;

using static BTModMerger.BTMMSchema;
using static BTModMerger.ToolBase;

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

        if (mod.Root!.Name != Elements.Diff)
        {
            Log.Error($"Mod ({modPath}) should be in diff format.");
            return;
        }

        var baseRoot = @base.Root!;

        var to = baseRoot.Name == Elements.FusedBase || asOverride && !baseRoot.HasAttributes ? new XDocument() : @base;
        mod = Linearizer.Apply(mod, modPath);

        var modRoot = mod.Root!;

        Apply(modRoot, baseRoot, to, CombineBTMMPaths(modPath, Elements.Diff));
        SaveResult(outputPath, to);
    }

    private static XDocument LoadMod(string modPath)
    {
        using var modFile = File.OpenRead(modPath);
        return XDocument.Load(modFile, LoadOptions.None);
    }

    private static void Apply(XElement diffElement, XElement from, XContainer to, string diffPath)
    {
        foreach (var child in diffElement.Elements())
        {
            if (child.Name != Elements.UpdateAttributes)
                continue;

            var target = child.GetBTMMPath()!;
            var childDiffPath = $"{CombineBTMMPaths(diffPath, child.Name)}({target})";

            var (fromTarget, toTarget) = GetTarget(target, from, to, diffPath, () => child.Attribute(Attributes.File)?.Value);

            foreach (var attr in child.Attributes())
            {
                var operation = attr.Name.Namespace;
                var attrTarget = attr.Name.LocalName;
                if (operation == AddNamespace) { toTarget.SetAttributeValue(attrTarget, attr.Value); }
                else if (operation == RemoveNamespace) { toTarget.Attribute(attrTarget)!.Remove(); }
                else if (operation == Namespace) { /* target path, etc */ }
                else throw new InvalidDataException($"Invalid attribute change operation (xmlns): {operation} at {childDiffPath}.{attr.Name.Fancify()}");
            }
        }

        var toRemove = new List<(XElement item, XContainer container, int amount, XElement request)>();
        var originalToChildren = to.Elements().ToArray();
        var normalizedChildren = originalToChildren.Select(e => XElementComparator.NormalizeElement(e)).ToArray();

        foreach (var child in diffElement.Elements(Elements.RemoveElement))
        {
            var target = child.GetBTMMPath()
                ?? throw new InvalidDataException($"btmm:RemoveElements without btmm:Path attribute at {diffPath}/{child.Name.Fancify()}");

            if (child.GetBTMMAmount() != 1)
            {
                Log.Error($"btmm:RemoveElements with btmm:Amount({child.GetBTMMAmount()}) at {diffPath}/{child.Name.Fancify()}, value ignored");
            }

            var (fromTarget, toTarget) = GetTarget(target, from, to, diffPath, () => child.Attribute(Attributes.File)?.Value);
            toRemove.Add((toTarget, toTarget.Parent!, 1, child));
        }

        foreach (var child in diffElement.Elements())
        {
            if (child.Name.Namespace != RemoveNamespace)
                continue;

            var item = new XElement(child)
            {
                Name = child.Name.LocalName,
            };
            item.Attribute(Attributes.Amount)?.Remove();
            item.Attribute(Attributes.Path)?.Remove();

            var target = child.GetBTMMPath();

            if (target is not null)
            {
                var (_, toTarget) = GetTarget(target, from, to, diffPath, () => child.Attribute(Attributes.File)?.Value);
                toRemove.Add((item, toTarget, child.GetBTMMAmount(), child));
            }
            else
            {
                toRemove.Add((item, to, child.GetBTMMAmount(), child));
            }
        }

        toRemove = toRemove
            .Select(ica => (XElementComparator.NormalizeElement(ica.item), ica.container, ica.amount, ica.request))
            .Deduplicate();

        var containerChildren = new Dictionary<XContainer, List<(XElement original, XElement normalized)>>();

        foreach (var (item, container, amount, request) in toRemove)
        {
            if (!containerChildren.TryGetValue(container, out var children))
            {
                children = container.Elements()
                    .Select(e => (e, XElementComparator.NormalizeElement(e)))
                    .ToList();
                containerChildren.Add(container, children);
            }

            for (var i = 0; i < amount; ++i)
            {
                var idx = children.FindIndex(pair => XNode.DeepEquals(item, pair.normalized));

                if (idx == -1)
                {
                    Log.Error($"Item to remove not found at {diffPath}/{request.Name.Fancify()}({request.GetBTMMPath()}/{item.Name.LocalName})");
                    break;
                }

                var (original, _) = children[idx];

                children.RemoveAt(idx);
                original.Remove();
            }
        }

        foreach (var child in diffElement.Elements())
        {
            if (child.Name.Namespace != XNamespace.None && child.Name.Namespace != AddNamespace)
                continue;

            var toAdd = new XElement(child);
            toAdd.Name = toAdd.Name.LocalName;
            toAdd.Attribute(Attributes.Amount)?.Remove();
            toAdd.Attribute(Attributes.Path)?.Remove();
            var amount = child.GetBTMMAmount();
            var target = child.GetBTMMPath();
            var targetContainer = to;

            if (target is not null)
            {
                var (_, toTarget) = GetTarget(target, from, to, diffPath, () => child.Attribute(Attributes.File)?.Value);
                targetContainer = toTarget;
            }

            for (var i = 0; i < amount; ++i)
                targetContainer.Add(toAdd);
        }
    }

    public static (XElement from, XElement to) GetTarget(string target, XElement from, XContainer to, string diffPath, Func<string?> filenameGetter)
    {
        var parts = SplitPath(target);
        XElement? targetElement = null;

        if (to is XDocument)
            to = to.Element(Elements.Override) ?? to;

        foreach (var part in parts)
        {
            var partCopy = part;
            var sss = ExtractSubscripts(ref partCopy, diffPath);
            var fromElems = from.Elements(partCopy);

            if (from.Name == Elements.FusedBase && BTMetadata.Instance.IndexByFilename.Contains(part))
            {
                var fileName = filenameGetter();
                fromElems = fromElems.Where(e => e.Attribute(Attributes.File)?.Value is null || e.Attribute(Attributes.File)?.Value == fileName);
            }

            fromElems = fromElems.FilterBySubscripts(sss, diffPath);

            var fromArray = fromElems.Take(2).ToArray();

            if (fromArray.Length != 1)
                throw new InvalidDataException($"A subscript has produced zero or more than one result at {diffPath}");

            var fromItem = fromArray[0];

            if (from == to)
            {
                targetElement = fromItem;
                to = fromItem;
            }
            else
            {
                var toElems = new[]
                    {
                        to.Elements(partCopy),
                        to.Elements(Elements.Override).SelectMany(e => e.Elements(partCopy)),
                    }
                    .SelectMany(e => e);

                toElems = toElems.FilterBySubscripts(sss, diffPath);
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
                        to.Add(new XElement(Elements.Override, targetElement));
                        to = targetElement;
                    }
                    else
                    {
                        if (to is XElement toElement && toElement.Name == Elements.Override)
                        {
                            targetElement = new(fromItem.Name);
                        }
                        else
                        {
                            targetElement = new(fromItem);
                            fromItem = targetElement;
                        }

                        to.Add(targetElement);
                        to = targetElement;
                    }
                }
            }

            from = fromItem;
        }

        if (targetElement is null)
            throw new InvalidDataException("Empty paths are not supported");

        return (from, targetElement);
    }
}
