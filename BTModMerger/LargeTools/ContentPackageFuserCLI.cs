using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.LargeTools;

public class ContentPackageFuserCLI(
    IFileIO fileio,
    IContentPackageFuser cpFuser
)
{
    public async Task Apply(string? packagePath, string targetLocation, string? packageRoot)
    {
        packageRoot ??= FindPackageRoot(packagePath);
        var package = fileio.OpenInput(ref packagePath);

        var (manifest, files) = cpFuser.Apply(package, path => fileio.OpenInputAsync(Path.Combine(packageRoot, path)));

        foreach (var (path, data) in files)
        {
            var target = Path.Combine(targetLocation, path);
            fileio.SaveResult(target, await data);
        }

        fileio.SaveResult(Path.Combine(targetLocation, FileNames.ContentPackage), await manifest);
    }

    internal static string FindPackageRoot(string? packagePath)
    {
        if (packagePath is null)
            throw new InvalidDataException("Package root and path both have not been provided");

        var packageRoot = new FileInfo(packagePath).Directory;

        while (packageRoot is not null && packageRoot.Name != "Barotrauma")
            packageRoot = packageRoot.Parent;

        if (packageRoot is null)
            throw new InvalidDataException("Package root has not been provided and cannot be calculated");

        return packageRoot.FullName;
    }
}
