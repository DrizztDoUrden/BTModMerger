using System.Xml.Linq;

using BTModMerger.Core.Interfaces;

using Microsoft.Extensions.Logging;

using static BTModMerger.Core.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Core;

public sealed class Applier(
    ILogger<Applier> logger,
    BTMetadata metadata
)
    : IApplier
{
    public void Apply(XElement diffElement, XContainer from, XContainer to, string diffPath)
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
                if (operation == AddNamespace) toTarget.SetAttributeCIS(attrTarget, attr.Value);
                else if (operation == RemoveNamespace) toTarget.FindBTAttributeCIS(attrTarget)!.Remove();
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
                logger.LogError("btmm:RemoveElements with btmm:Amount({amount}) at {diffPath}/{childName}, value ignored",
                    child.GetBTMMAmount(),
                    diffPath,
                    child.Name.Fancify()
                );

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
                toRemove.Add((item, to, child.GetBTMMAmount(), child));
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
                    logger.LogError("Item to remove not found at {diffPath}/{requestName}({requestPath}/{itemName})",
                        diffPath,
                        request.Name.Fancify(),
                        request.GetBTMMPath(),
                        item.Name.LocalName
                    );
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

    public (XContainer from, XElement to) GetTarget(string target, XContainer from, XContainer to, string diffPath, Func<string?> filenameGetter)
    {
        var parts = SplitPath(target);
        XElement? targetElement = null;

        if (to is XDocument)
            to = to.Element(Elements.Override) ?? to;

        foreach (var part in parts)
        {
            var partCopy = part;
            var sss = ExtractSubscripts(ref partCopy, diffPath);
            var isFused = from.Elements(Elements.FusedBase).Any();

            var fromElems = isFused
                ? from.Elements(Elements.FusedBase).Single().ElementsCIS(partCopy)
                : from.ElementsCIS(partCopy);

            if (isFused && metadata.IndexByFilename.Contains(part))
            {
                var fileName = filenameGetter();
                fromElems = fromElems
                    .Where(e => e.Attribute(Attributes.File)?.Value is null || e.Attribute(Attributes.File)?.Value == fileName);
            }

            fromElems = fromElems.FilterBySubscripts(sss, diffPath, metadata);

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

                toElems = toElems.FilterBySubscripts(sss, diffPath, metadata);
                var toArray = toElems.Take(2).ToArray();

                if (toArray.Length == 1)
                {
                    targetElement = toArray[0];
                    to = targetElement;
                }
                else
                {
                    // Means we are in override mode
                    if (to is XDocument && !fromItem.HasAttributes)
                    {
                        var @override = new XElement(Elements.Override);
                        to.Add(@override);
                        to = @override;
                    }

                    if (to is XElement toElement && toElement.Name == Elements.Override && !fromItem.HasAttributes)
                        targetElement = new(fromItem.Name);
                    else
                    {
                        targetElement = new(fromItem);
                        fromItem = targetElement;
                    }

                    to.Add(targetElement);
                    to = targetElement;

                }
            }

            from = fromItem;
        }

        if (targetElement is null)
            throw new InvalidDataException("Empty paths are not supported");

        return (from, targetElement);
    }
}
