using BTModMerger.Core.Interfaces;
using BTModMerger.LargeTools;
using BTModMerger.Tests.Mockers;

using System.Xml.Linq;

using static BTModMerger.Core.Schema.BTMMSchema;
using static BTModMerger.Tests.CLI.CLITestHelpers;

namespace BTModMerger.Tests.CLI;

public class ModApplierCLI_Tests
{
    private class ModApplierMocker : IModApplier
    {
        public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files, IEnumerable<string> copies) Apply(XDocument basePackage, Func<string, Task<XDocument>> baseFiles, XDocument modDiff, Func<string, Task<XDocument>> modFiles)
        {
            return (
                Task.FromResult<XDocument>(new(ModDiff())),
                [
                    ("file0", Task.FromResult<XDocument>(new()))
                ],
                []
            );
        }
    }

    public static IModApplier MakeMocker() => new ModApplierMocker();

    private ModApplierCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker()
    );

    [Fact]
    public async Task Empty()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var cpRoot = new DirectoryInfo("cp").FullName;
        var modRoot = new DirectoryInfo("mod").FullName;
        var diffRoot = new DirectoryInfo("diff").FullName;

        fileio.Directories.Add(cpRoot);
        fileio.Directories.Add(modRoot);
        fileio.Directories.Add(diffRoot);

        fileio.ChildFiles.Add(cpRoot, []);
        fileio.ChildFiles.Add(modRoot, []);

        var cp = MakeValidInput(fileio, Path.Combine(cpRoot, FileNames.ContentPackage), root: ContentPackage());
        var diff = MakeValidInput(fileio, Path.Combine(diffRoot, FileNames.ModDiff), root: ModDiff());
        var mod = MakeValidOutput(fileio, Path.Combine(modRoot, "filelist.xml"));

        await tool.Apply(cpRoot, diffRoot, modRoot);

        ValidateInput(cp);
    }
}
