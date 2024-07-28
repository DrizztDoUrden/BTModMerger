using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using Microsoft.Extensions.Logging;

using static BTModMerger.Core.Schema.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Core.Tools;

public class Simplifier(
    ILogger<Simplifier> logger
)
    : ISimplifier
{
    public XDocument Apply(XDocument input, string inputPath, in ISimplifier.Options options, XElement? conflictsRoot)
    {
        var to = new XDocument(input);
        var dbgPath = $"{inputPath}:Diff";
        Simplify(to.Root!, dbgPath, options, conflictsRoot);
        return to;
    }

    private void Simplify(XElement output, string dbgPath, in ISimplifier.Options options, XElement? conflictsRoot)
    {
        dbgPath = CombineBTMMPaths(dbgPath, output.Name);

        if (output.Name == Elements.Diff)
        {
            foreach (var child in output.Elements().ToArray())
            {
                if (child.Parent is null)
                    continue; // It has been removed already
                Simplify(child, dbgPath, options, conflictsRoot);
            }
            return;
        }

        if (output.Name == Elements.Into)
        {
            var ownPath = output.GetBTMMPath();

            while (true)
            {
                foreach (var child in output.Elements(Elements.Into).ToArray())
                {
                    if (child.Parent is null)
                        continue; // It has been removed already
                    Simplify(child, dbgPath, options, conflictsRoot);
                }

                foreach (var child in output.Elements().ToArray())
                {
                    if (child.Parent is null || child.Name == Elements.Into)
                        continue; // It has been processed already
                    Simplify(child, dbgPath, options, conflictsRoot);
                }

                var children = output.Elements().Take(2).ToArray();

                if (children.Length != 1 ||
                    children[0].Name != Elements.Into &&
                    children[0].Name != Elements.UpdateAttributes)
                    break;

                var exParent = output.Parent!;
                var theOnlyChild = children[0];
                output.Remove();
                theOnlyChild.SetAttributeSorting(Attributes.Path, CombineBTMMPaths(ownPath, theOnlyChild.GetBTMMPath()));
                exParent.Add(theOnlyChild);
                output = theOnlyChild;

                if (output.Name != Elements.Into)
                    break;
            }

            if (output.Name == Elements.Into)
            {
                if (!output.HasElements)
                {
                    if (output.HasAttributes)
                    {
                        var parent = output.Parent!;

                        var updateAttrs = parent.Elements(Elements.UpdateAttributes)
                            .Where(e => e.GetBTMMPath() == ownPath)
                            .FirstOrDefault();

                        if (updateAttrs is null)
                        {
                            output.Name = Elements.UpdateAttributes;
                            return;
                        }

                        CopyAttributes(output, updateAttrs, dbgPath, options.conflictHandlingPolicy, conflictsRoot);
                    }

                    output.Remove();
                }

                return;
            }
        }

        if (output.Name == Elements.UpdateAttributes)
        {
            var parent = output.Parent!;
            var ownPath = output.GetBTMMPath();

            var clones = parent.Elements(Elements.UpdateAttributes)
                .Where(e => e != output && e.GetBTMMPath() == ownPath)
                .ToArray();

            foreach (var clone in clones)
            {
                CopyAttributes(clone, output, dbgPath, options.conflictHandlingPolicy, conflictsRoot);
                clone.Remove();
            }

            return;
        }

        if (output.Name == Elements.RemoveElement)
        {
            var ownPath = output.GetBTMMPath();
            if (ownPath is null)
            {
                logger.LogError("btmm:RemoveElements lacks btmm:Path at {dbgPath}", dbgPath);
                return;
            }

            var parent = output.Parent!;
            var clones = parent.Elements(Elements.RemoveElement)
                .Where(e => e != output && e.GetBTMMPath() == ownPath)
                .ToArray();

            foreach (var clone in clones)
                clone.Remove();

            return;
        }

        if (output.Name.Namespace == RemoveNamespace)
        {
            var ownPath = output.GetBTMMPath();
            var normalizedContent = NormalizeContent(output);

            var clones = output.Parent!.Elements()
                .Where(e => e.Name.Namespace == RemoveNamespace)
                .Where(e => e.GetBTMMPath() == ownPath)
                .Where(e => XNode.DeepEquals(NormalizeContent(e), normalizedContent))
                .ToArray();

            // This already includes self, so we do not need to add it.
            var newAmount = clones.Sum(e => e.GetBTMMAmount());
            if (newAmount != 1)
                output.SetAttributeValue(Attributes.Amount, newAmount);
            else
                output.Attribute(Attributes.Amount)?.Remove();

            foreach (var clone in clones)
                if (clone != output)
                    clone.Remove();

            return;
        }

        switch (options.AddNamespacePolicy)
        {
            case AddNamespacePolicy.Add:
                if (output.Name.Namespace == XNamespace.None)
                    output.Name = AddNamespace + output.Name.LocalName;
                break;
            case AddNamespacePolicy.Remove:
                if (output.Name.Namespace == AddNamespace)
                    output.Name = output.Name.LocalName;
                break;
            case AddNamespacePolicy.Preserve:
                break;
        }
    }

    private XElement NormalizeContent(XElement element)
    {
        var content = new XElement(element);
        content.Name = content.Name.LocalName;
        content.Attribute(Attributes.Path)?.Remove();
        content.Attribute(Attributes.Amount)?.Remove();
        return XElementComparator.NormalizeElement(content);
    }

    public void CopyAttributes(XElement from, XElement to, string dbgPath, ConflictHandlingPolicy conflictHandlingPolicy, XElement? conflictsRoot)
    {
        var conflicts = new Dictionary<XName, string>();

        foreach (var attr in from.Attributes())
        {
            if (attr.Name == Attributes.Path)
                continue;

            XAttribute? existing;

            void HandleConflict()
            {
                if (existing is not null)
                    switch (conflictHandlingPolicy)
                    {
                        case ConflictHandlingPolicy.Override:
                            logger.LogWarning("Duplicate attributes at {dbgPath}.{attrName}: <{attrValue}> vs <{existingValue}>, ignoring the later one",
                                dbgPath,
                                attr.Name.Fancify(),
                                attr.Value,
                                existing.Value
                            );
                            conflicts.Add(attr.Name, attr.Value);
                            break;
                        case ConflictHandlingPolicy.Error:
                            throw new InvalidDataException($"Duplicate attributes at {dbgPath}.{attr.Name.Fancify()}: <{attr.Value}> vs <{existing.Value}>.");
                    }
            }

            existing = to.Attribute(attr.Name);
            HandleConflict();
            existing = to.Attribute((attr.Name.Namespace == AddNamespace ? RemoveNamespace : AddNamespace) + attr.Name.LocalName);
            HandleConflict();

            to.SetAttributeValue(attr.Name, attr.Value);
        }

        if (conflictsRoot is not null && conflicts.Count > 0)
        {
            var pathToElement = new List<string>();
            for (var current = from; current is not null && current.Name != Elements.Diff; current = current.Parent)
            {
                string? pathPart = null;
                var elementPath = current.GetBTMMPath();
                if (elementPath is not null)
                    pathPart = pathPart is not null ? CombineBTMMPaths(elementPath, pathPart) : elementPath;

                if (pathPart is not null)
                    pathToElement.Add(pathPart);
            }
            pathToElement.Reverse();

            var relativeLocation = string.Join('/', pathToElement);

            var update = conflictsRoot.Elements(Elements.UpdateAttributes)
                .Where(e => e.GetBTMMPath() == relativeLocation)
                .FirstOrDefault();

            if (update is null)
            {
                update = UpdateAttributes();
                update.SetAttributeValue(Attributes.Path, relativeLocation);
                conflictsRoot.Add(update);
            }

            foreach (var conflict in conflicts)
                update.SetAttributeValue(conflict.Key, conflict.Value);
        }

        to.SortAttributes();
    }
}
