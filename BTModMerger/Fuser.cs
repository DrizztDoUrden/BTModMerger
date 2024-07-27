using System.Xml.Linq;
using Microsoft.Extensions.Logging;

using static BTModMerger.BTMMSchema;
using static BTModMerger.CLI.CLI;
using static BTModMerger.ToolBase;

namespace BTModMerger;

public sealed class Fuser(
    ILogger<Fuser> logger,
    BTMetadata metadata,
    Linearizer linearizer,
    Delinearizer delinearizer,
    Simplifier simplifier
)
{
    public void Apply(string[] partPaths, Stream? cin, bool partsFromCin, string? outputPath, bool delinearize, bool skipSimplifying, in Simplifier.Options simplifierOptions, ConflictsFileInfo? conflicts)
    {
        List<(string path, XDocument xml)> parts = partPaths
            .Select(path => (path, XDocument.Load(path)))
            .ToList();

        if (partsFromCin)
        {
            using var cinReader = new StreamReader(cin!);
            for (var path = cinReader.ReadLine(); !string.IsNullOrEmpty(path); path = cinReader.ReadLine())
                parts.Add((path, XDocument.Load(path)));
        }
        else if (cin is not null)
        {
            parts.Add(("cin", XDocument.Load(cin)));
        }

        if (parts.Count < 2)
        {
            logger.LogError("Less than two files provided to fuse.");
            return;
        }

        var partRootNames = parts
            .SelectMany(p => p.xml.Elements())
            .Select(e => e.Name)
            .Distinct()
            .ToArray();

        var notDiffs = partRootNames.Any(name => name != Elements.Diff);
        if (notDiffs && partRootNames.Any(name => name == Elements.Diff))
        {
            logger.LogError("Fusing diffs with base files is not supported. Maybe you wanted to apply them?");
            return;
        }

        parts = parts
            .Select(part => (part.path, linearizer.Apply(part.xml, part.path)))
            .ToList();

        var toRoot = notDiffs ? FusedBase() : Diff();
        var to = new XDocument(toRoot);
        XDocument? conflictsDocument = null;

        if (conflicts is not null)
        {
            var existed = !conflicts.Override && conflicts.Path.Exists;
            conflictsDocument = existed ? XDocument.Load(conflicts.Path.FullName) : new XDocument(Diff());
        }

        foreach (var (path, xml) in parts)
            Apply(toRoot, xml.Elements().Single(), path + ":", new FileInfo(path).Name);
        if (!skipSimplifying)
            to = simplifier.Apply(to, "<temporary>", simplifierOptions, conflictsDocument?.Root);
        if (delinearize)
            to = delinearizer.Apply(to, "<temporary>");

        SaveResult(outputPath, to);

        if (conflicts is not null)
        {
            if (conflicts.Delinearize)
                conflictsDocument = delinearizer.Apply(conflictsDocument!, conflicts.Path.FullName);
            conflictsDocument?.Save(conflicts!.Path.FullName);
        }
    }

    public void Apply(XElement to, XElement part, string dbgPath, string filename)
    {
        var nextPath = CombineBTMMPaths(dbgPath, part.Name);

        if (part.Name == Elements.Diff || part.Name == Elements.FusedBase)
        {
            foreach (var child in part.Elements())
                Apply(to, child, nextPath, filename);
            return;
        }

        if (to.Name == Elements.FusedBase)
        {
            if (metadata.IndexByFilename.Contains(part.Name.Fancify().ToLower()))
            {
                var copy = new XElement(part);
                if (copy.Attribute(Attributes.File) is null)
                    copy.SetAttributeValue(Attributes.File, filename);
                to.Add(copy);
                return;
            }
        }

        if (metadata.Partial.Contains(part.Name.Fancify().ToLower()))
        {
            var target = to.Elements(part.Name)
                .SingleOrDefault();

            if (target is null)
            {
                to.Add(new XElement(part));
            }
            else
            {
                foreach (var item in part.Elements())
                    target.Add(item);
            }

            return;
        }

        to.Add(part);
    }
}
