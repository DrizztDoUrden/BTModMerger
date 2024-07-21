using System.Xml.Linq;

using static BTModMerger.BTMMSchema;
using static BTModMerger.CLI;
using static BTModMerger.ToolBase;

namespace BTModMerger;

static internal class Simplifier
{
    public readonly record struct Options(AddNamespacePolicy AddNamespacePolicy, ConflictHandlingPolicy conflictHandlingPolicy);

    public static void Apply(string? inputPath, string? outputPath, in Options options, ConflictsFileInfo? conflicts)
    {
        var baseFile = string.IsNullOrWhiteSpace(inputPath)
            ? Console.OpenStandardInput()
            : File.OpenRead(inputPath);

        inputPath ??= "cin";
        var input = XDocument.Load(baseFile, LoadOptions.None);

        if (baseFile is FileStream)
            baseFile.Dispose();

        if (input.Root is null || input.Root.Name != Elements.Diff)
        {
            Log.Error($"({inputPath}) should be a BTMM diff xml.");
            return;
        }

        XDocument? conflictsDocument = null;

        if (conflicts is not null)
        {
            var existed = !conflicts.Override && conflicts.Path.Exists;
            conflictsDocument = existed ? XDocument.Load(conflicts.Path.FullName) : new XDocument(Diff());
        }

        var to = Apply(input, inputPath, options, conflictsDocument?.Root!);
        SaveResult(outputPath, to);

        if (conflicts is not null)
        {
            if (conflicts.Delinearize)
                conflictsDocument = Delinearizer.Apply(conflictsDocument!, conflicts.Path.FullName);
            conflictsDocument!.Save(conflicts!.Path.FullName);
        }
    }

    public static XDocument Apply(XDocument input, string? inputPath, in Options options, XElement? conflictsRoot)
    {
        var to = new XDocument(input);
        var dbgPath = $"{inputPath}:Diff";
        Simplify(to.Root!, dbgPath, options, conflictsRoot);
        return to;
    }

    private static void Simplify(XElement output, string dbgPath, in Options options, XElement? conflictsRoot)
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
                    if (child.Parent is null || child.Name != Elements.Into)
                        continue; // It has been processed already
                    Simplify(child, dbgPath, options, conflictsRoot);
                }

                if (output.Elements().Take(2).Count() != 1 || output.Elements().FirstOrDefault()!.Name != Elements.Into)
                    break;

                output.Remove();
                output = output.Elements().FirstOrDefault()!;
            }

            if (!output.HasElements)
                output.Remove();

            return;
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

            var into = parent.Elements(Elements.Into)
                .Where(e => e.GetBTMMPath() == ownPath && e.HasElements)
                .FirstOrDefault();

            if (into is not null)
            {
                foreach (var attr in output.Attributes())
                {
                    if (attr.Name == Attributes.Path)
                        continue;

                    CopyAttributes(output, into, dbgPath, options.conflictHandlingPolicy, conflictsRoot);
                }

                output.Remove();
            }

            return;
        }

        if (output.Name == Elements.RemoveElement)
        {
            var ownPath = output.GetBTMMPath();
            if (ownPath is null)
            {
                Log.Error($"btmm:RemoveElements lacks btmm:Path at {dbgPath}");
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

    private static XElement NormalizeContent(XElement element)
    {
        var content = new XElement(element);
        content.Name = content.Name.LocalName;
        content.Attribute(Attributes.Path)?.Remove();
        content.Attribute(Attributes.Amount)?.Remove();
        return XElementComparator.NormalizeElement(content);
    }

    public static void CopyAttributes(XElement from, XElement to, string dbgPath, ConflictHandlingPolicy conflictHandlingPolicy, XElement? conflictsRoot)
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
                {
                    switch (conflictHandlingPolicy)
                    {
                        case ConflictHandlingPolicy.Override:
                            Log.Warning($"Duplicate attributes at {dbgPath}.{attr.Name.Fancify()}: <{attr.Value}> vs <{existing.Value}>, ignoring the later one");
                            conflicts.Add(attr.Name, attr.Value);
                            break;
                        case ConflictHandlingPolicy.Error:
                            throw new InvalidDataException($"Duplicate attributes at {dbgPath}.{attr.Name.Fancify()}: <{attr.Value}> vs <{existing.Value}>.");
                    }
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
                string? selfPathPart = null;
                var curName = current.Name;

                if (curName.Namespace == XNamespace.None)
                {
                    var curId = current.GetBTIdentifier();

                    // This index calculation is probably completely incorrect
                    var clones = (current.Parent
                        ?.Elements(curName) ?? [])
                        .Where(e => e.GetBTIdentifier() == curId)
                        .ToList();

                    var index = clones.Count > 1 ? clones.IndexOf(current) : -1;

                    selfPathPart = curName.ToString();
                    if (curId is not null) selfPathPart += $"[{curId}]";
                    if (index != -1) selfPathPart += $"[{index}]";
                }

                var pathPart = selfPathPart is not null ? selfPathPart : null;
                var elementPath = from.GetBTMMPath();
                if (elementPath is not null)
                {
                    pathPart = pathPart is not null ? CombineBTMMPaths(elementPath, pathPart) : elementPath;
                }

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
    }
}
