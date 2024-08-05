using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;
using Microsoft.Extensions.Logging;

using PowerArgs.Samples;

using static BTModMerger.Core.Schema.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Core.Tools;

public class Differ(
    BTMetadata metadata
)
    : IDiffer
{
    public XDocument Apply(XDocument @base, string basePath, XDocument mod, string modPath, bool alwaysOverride)
    {
        var ret = new XDocument(Diff());

        if (@base.Root is null)
            throw new InvalidDataException("Base document should not be empty");

        if (mod.Root is null)
            throw new InvalidDataException("Mod document should not be empty");

        var subPath = FormPath(@base.Root, @base);
        var modSubPath = FormPath(mod.Root, mod);
        ProcessRootElement(@base.Root, @base, CombineBTMMPaths(basePath, subPath), "", mod.Root, CombineBTMMPaths(modPath, modSubPath), alwaysOverride, alwaysOverride, ret.Root!);

        return ret;
    }

    private string FormPath(XElement element, XContainer parent)
    {
        var (id, idx) = FindItem(element, parent);
        return FormPath(element.Name, id, idx);
    }

    private static string FormPath(XName childName, string? childId, int childIndex)
    {
        var childPath = childName.Fancify();
        if (childId is not null) childPath += $"[@{childId}]";
        if (childIndex != -1) childPath += $"[{childIndex}]";
        return childPath;
    }

    private (string? id, int idx) FindItem(XElement element, XContainer parent)
    {
        var name = element.Name;
        var id = element.GetBTIdentifier(metadata);

        var idx = -1;

        if (parent is not null)
        {
            var clones = parent
                .Elements(name)
                .Where(e => id is null || e.GetBTIdentifier(metadata) == id)
                .ToImmutableArray();

            if (element.IsIndexed(metadata) || clones.Length > 1)
                idx = clones.IndexOf(element);
        }

        return (id, idx);
    }

    private (XElement? found, string? subPath) FindTargetElement(XElement searched, XContainer searchedParent, XContainer searchTarget, string fromPath, string targetPath)
    {
        var (id, idx) = FindItem(searched, searchedParent);

        var options = searchTarget
            .Elements(searched.Name)
            .FilterBy((id, idx), metadata)
            .Take(2).ToImmutableArray();

        if (options.Length > 1)
            throw new InvalidDataException($"Multiple potential targets found in <{targetPath}> when searching for {fromPath}.");

        return (options.Length == 1 ? options[0] : null, FormPath(searched.Name, id, idx));
    }

    private void ProcessRootElement(XElement @base, XContainer baseContainer, string basePath, string btmmPath, XElement mod, string modPath, bool @override, bool alwaysOverride, XElement target)
    {
        if (mod.IsBTOverride(metadata))
        {
            ProcessRootElementChildren(@base, baseContainer, basePath, btmmPath, mod, modPath, true, alwaysOverride, target);
            return;
        }

        btmmPath = CombineBTMMPaths(btmmPath, FormPath(mod, mod.Parent!));

        if (mod.IsRootContainer(metadata))
        {
            ProcessRootElementChildren(@base, baseContainer, basePath, btmmPath, mod, modPath, @override, alwaysOverride, target);
            return;
        }

        ProcessOverride(@base, baseContainer, basePath, btmmPath, mod, modPath, @override, target);
    }

    private void ProcessRootElementChildren(XElement @base, XContainer baseContainer, string basePath, string btmmPath, XElement mod, string modPath, bool @override, bool alwaysOverride, XElement target)
    {
        foreach (var child in mod.Elements())
        {
            var childPath = CombineBTMMPaths(modPath, FormPath(child, mod));

            if (!child.IsBTOverride(metadata))
            {
                var (found, _) = FindTargetElement(child, mod, @base, childPath, basePath);

                if (found is null)
                {
                    target.Add(AddElements(btmmPath, child));
                    continue;
                }

                @base = found;
            }

            ProcessRootElement(@base, baseContainer, basePath, btmmPath, child, childPath, @override, alwaysOverride, target);
        }

        if (!alwaysOverride && (@base.Ancestors().LastOrDefault() ?? @base).Name != Elements.FusedBase)
        {
            foreach (var baseChild in @base.Elements())
            {
                var childPath = CombineBTMMPaths(basePath, FormPath(baseChild, @base));
                var (found, subPath) = FindTargetElement(baseChild, @base, mod, modPath, childPath);

                if (found is not null)
                    // Processed in the first loop
                    continue;

                target.Add(RemoveElement(CombineBTMMPaths(btmmPath, subPath)));
            }
        }
    }

    private void ProcessOverride(XElement @base, XContainer baseContainer, string basePath, string btmmPath, XElement mod, string modPath, bool @override, XElement target)
    {
        ProcessOverriddenAttributes(@base, btmmPath, mod, target);

        void ProcessTrickies(ref readonly ImmutableArray<(XElement item, int count)> from, ref readonly ImmutableArray<(XElement item, int count)> to, bool isDirect)
        {
            foreach (var (item, fromCount) in from)
            {
                var (toItem, toCount) = to
                    .Where(toTricky => XNode.DeepEquals(item, toTricky!.item))
                    .Cast<(XElement?, int)>()
                    .FirstOrDefault((null, 0));

                if (!isDirect && toItem is not null)
                    continue;

                var delta = (fromCount - toCount);
                if (!isDirect) delta = -delta;

                switch (delta)
                {
                    case > 0: target.Add(AddElements(btmmPath, item, delta)); break;
                    case < 0: target.Add(RemoveElement(btmmPath, item, -delta)); break;
                }
            }
        }

        var modTrickies = mod.Elements()
            .Where(e => e.IsTricky(metadata))
            .Select(XElementComparator.NormalizeElement)
            .Deduplicate()
            .ToImmutableArray();

        var baseTrickies = @base.Elements()
            .Where(e => e.IsTricky(metadata))
            .Select(XElementComparator.NormalizeElement)
            .Deduplicate()
            .ToImmutableArray();

        ProcessTrickies(in modTrickies, in baseTrickies, true);
        ProcessTrickies(in baseTrickies, in modTrickies, false);

        foreach (var modChild in mod.Elements())
        {
            if (modChild.IsTricky(metadata))
                continue;

            var (baseTarget, subPath) = FindTargetElement(modChild, mod, @base, modPath, basePath);

            if (baseTarget is null)
            {
                target.Add(AddElements(btmmPath, modChild));
                continue;
            }

            var childBTMMPath = CombineBTMMPaths(btmmPath, subPath);
            ProcessOverride(baseTarget, @base, basePath, childBTMMPath, modChild, modPath, @override, target);
        }

        foreach (var baseChild in @base.Elements())
        {
            if (baseChild.IsTricky(metadata))
                continue;

            var (modTarget, subPath) = FindTargetElement(baseChild, @base, mod, basePath, modPath);

            if (modTarget is not null)
                // processed in the first loop
                continue;

            target.Add(RemoveElement(CombineBTMMPaths(btmmPath, subPath!)));
        }
    }

    private void ProcessOverriddenAttributes(XElement @base, string btmmPath, XElement mod, XElement target)
    {
        XElement? updateAttributes = null;

        XElement GetUpdateAttributes()
        {
            if (updateAttributes is not null) return updateAttributes;
            updateAttributes = UpdateAttributes();
            updateAttributes.SetAttributeSorting(Attributes.Path, btmmPath);
            target.Add(updateAttributes);
            return updateAttributes;
        }

        var toAdd = mod.Attributes()
            .Where(attr => attr.Value != @base.GetBTAttributeCIS(attr.Name));
        var toRemove = @base.Attributes()
            .Where(attr => mod.FindBTAttributeCIS(attr.Name) is null);

        foreach (var attr in toAdd)
            GetUpdateAttributes().SetAttributeCIS(AddNamespace + attr.Name.LocalName, attr.Value);
        foreach (var attr in toRemove)
            GetUpdateAttributes().SetAttributeCIS(RemoveNamespace + attr.Name.LocalName, "");
    }
}
