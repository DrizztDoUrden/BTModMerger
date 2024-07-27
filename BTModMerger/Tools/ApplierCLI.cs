using System.Xml.Linq;
using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Tools;

public class ApplierCLI(
    IFileIO fileio,
    IApplier applier,
    ILinearizer linearizer
)
{
    public void Apply(string? basePath, string modPath, string? outputPath, bool asOverride)
    {
        var @base = fileio.OpenInput(ref basePath);
        var mod = fileio.OpenInput(ref modPath);

        if (mod.Root!.Name != Elements.Diff)
            throw new InvalidDataException($"Mod ({modPath}) should be in diff format.");

        var baseRoot = @base.Root!;

        var to = baseRoot.Name == Elements.FusedBase || asOverride && !baseRoot.HasAttributes ? new XDocument() : @base;
        mod = linearizer.Apply(mod, modPath);

        var modRoot = mod.Root!;

        applier.Apply(modRoot, baseRoot, to, CombineBTMMPaths(modPath, Elements.Diff));
        fileio.SaveResult(outputPath, to);
    }
}
