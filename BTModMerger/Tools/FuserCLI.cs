using BTModMerger.Core;
using BTModMerger.Core.Interfaces;
using System.Xml.Linq;

using static BTModMerger.Core.BTMMSchema;

namespace BTModMerger.Tools;

public class FuserCLI(
    IFileIO fileio,
    IFuser fuser,
    ILinearizer linearizer,
    IDelinearizer delinearizer,
    ISimplifier simplifier
)
{
    public void Apply(string[] partPaths, bool processCin, bool partsFromCin, string? outputPath, bool delinearize, bool skipSimplifying, in ISimplifier.Options simplifierOptions, ConflictsFileInfo? conflicts)
    {
        List<(string path, XDocument xml)> parts = partPaths
            .Select(path => (path, fileio.OpenInput(path)))
            .ToList();

        if (partsFromCin)
        {
            using var cinReader = new StreamReader(fileio.OpenStandardInputStream());
            for (var path = cinReader.ReadLine(); !string.IsNullOrEmpty(path); path = cinReader.ReadLine())
                parts.Add((path, fileio.OpenInput(path)));
        }
        else if (processCin)
            parts.Add(("cin", fileio.OpenInput()));

        if (parts.Count < 2)
            throw new InvalidDataException("Less than two files provided to fuse.");

        var partRootNames = parts
            .SelectMany(p => p.xml.Elements())
            .Select(e => e.Name)
            .Distinct()
            .ToArray();

        var notDiffs = partRootNames.Any(name => name != Elements.Diff);
        if (notDiffs && partRootNames.Any(name => name == Elements.Diff))
            throw new InvalidDataException("Fusing diffs with base files is not supported. Maybe you wanted to apply them?");

        parts = parts
            .Select(part => (part.path, linearizer.Apply(part.xml, part.path)))
            .ToList();

        var toRoot = notDiffs ? FusedBase() : Diff();
        var to = new XDocument(toRoot);
        XDocument? conflictsDocument = null;

        if (conflicts is not null)
        {
            var existed = !conflicts.Override && fileio.FileExists(conflicts.Path.FullName);
            conflictsDocument = existed ? fileio.OpenInput(conflicts.Path.FullName) : new XDocument(Diff());
        }

        foreach (var (path, xml) in parts)
            fuser.Apply(toRoot, xml.Elements().Single(), path + ":", new FileInfo(path).Name);
        if (!skipSimplifying)
            to = simplifier.Apply(to, "<temporary>", simplifierOptions, conflictsDocument?.Root);
        if (delinearize)
            to = delinearizer.Apply(to, "<temporary>");
        fileio.SaveResult(outputPath, to);

        if (conflicts is not null)
        {
            if (conflicts.Delinearize)
                conflictsDocument = delinearizer.Apply(conflictsDocument!, conflicts.Path.FullName);
            fileio.SaveResult(conflicts.Path.FullName, conflictsDocument);
        }
    }
}
