using Microsoft.Extensions.Logging;
using System.Xml.Linq;

using BTModMerger.Core;
using BTModMerger.Core.Interfaces;

namespace BTModMerger.Tools;

using static Core.BTMMSchema;

public class SimplifierCLI(
    IFileIO fileio,
    ISimplifier simplifier,
    IDelinearizer delinearizer
)
{
    public void Apply(string? inputPath, string? outputPath, in ISimplifier.Options options, ConflictsFileInfo? conflicts)
    {
        var input = fileio.OpenInput(ref inputPath);

        if (input.Root is null || input.Root.Name != Elements.Diff)
            throw new InvalidDataException($"({inputPath}) should be a BTMM diff xml.");

        XDocument? conflictsDocument = null;

        if (conflicts is not null)
        {
            var existed = !conflicts.Override && fileio.FileExists(conflicts.Path.FullName);
            conflictsDocument = existed ? fileio.OpenInput(conflicts.Path.FullName) : new XDocument(Diff());
        }

        var to = simplifier.Apply(input, inputPath, options, conflictsDocument?.Root!);
        fileio.SaveResult(outputPath, to);

        if (conflicts is not null)
        {
            if (conflicts.Delinearize)
                conflictsDocument = delinearizer.Apply(conflictsDocument!, conflicts.Path.FullName);
            fileio.SaveResult(conflicts.Path.FullName, conflictsDocument);
        }
    }
}
