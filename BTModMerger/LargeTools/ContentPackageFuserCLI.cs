using System.Xml.Linq;

using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.LargeTools;

public class ContentPackageFuserCLI(
    IFileIO fileio,
    IContentPackageFuser cpFuser
)
{
    public async Task Apply(string? packagePath, string targetLocation, int threads, string? packageRoot)
    {
        packageRoot ??= FindPackageRoot(packagePath);
        var package = fileio.OpenInput(ref packagePath);
        var manifest = new XDocument(ContentPackage());

        await foreach (var (path, kind, data) in cpFuser.Apply(package, path => fileio.OpenInput(Path.Combine(packageRoot, path)), threads))
        {
            var target = Path.Combine(targetLocation, path);
            fileio.SaveResult(target, data);

            manifest.Root!.Add(new XElement(kind, new XAttribute(Attributes.Path, path)));
        }

        fileio.SaveResult(Path.Combine(targetLocation, "BTMMContentPackage.xml"), manifest);
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
