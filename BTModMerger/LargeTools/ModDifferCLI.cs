using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.LargeTools;

public class ModDifferCLI(
    IFileIO fileio,
    IModDiffer modDiffer
)
{
    public async Task Apply(string contentPackagePath, string modPath, string targetLocation, bool alwaysOverride)
    {
        var cp = await fileio.OpenBTMMPackage(contentPackagePath, FileNames.ContentPackage);
        var mod = await fileio.OpenBTMMPackage(modPath, "filelist.xml");

        var diff = modDiffer.Apply(
            cp.manifest, cp.files,
            mod.manifest, mod.files,
            fileio.GetFiles(modPath, "*.xml", SearchOption.AllDirectories),
            alwaysOverride
        );

        foreach (var (path, data) in diff.files)
            await fileio.SaveResultAsync(Path.Combine(targetLocation, path), await data);

        var manifest = await diff.manifest;

        await fileio.SaveResultAsync(Path.Combine(targetLocation, FileNames.ContentPackage), manifest);
    }
}
