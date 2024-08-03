using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.LargeTools;

public class ModApplierCLI(
    IFileIO fileio,
    IModApplier modApplier
)
{
    public async Task Apply(string contentPackagePath, string diffPath, string targetLocation)
    {
        var cp = await fileio.OpenBTMMPackage(contentPackagePath, FileNames.ContentPackage);
        var mod = await fileio.OpenBTMMPackage(diffPath, FileNames.ModDiff);
        var diff = modApplier.Apply(
            cp.manifest, cp.files,
            mod.manifest, mod.files
        );

        foreach (var (path, data) in diff.files)
            await fileio.SaveResultAsync(Path.Combine(targetLocation, path), await data);

        var manifest = await diff.manifest;

        await fileio.SaveResultAsync(Path.Combine(targetLocation, "filelist.xml"), manifest);
    }
}
