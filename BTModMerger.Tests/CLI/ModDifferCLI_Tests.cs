using BTModMerger.Core.Interfaces;
using BTModMerger.LargeTools;
using BTModMerger.Tests.Mockers;

using System.Xml.Linq;

using static BTModMerger.Core.Schema.BTMMSchema;
using static BTModMerger.Tests.CLI.CLITestHelpers;

namespace BTModMerger.Tests.CLI;

public class ModDifferCLI_Tests
{
    private class ModDifferMocker : IModDiffer
    {
        public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(XDocument basePackage, Func<string, Task<XDocument>> baseFiles, XDocument modFilelist, Func<string, Task<XDocument>> modFiles, IEnumerable<string> allModXmlFiles, bool alwaysOverride)
        {
            return (
                Task.FromResult<XDocument>(new(ModDiff())),
                new[]
                {
                    ("file0", Task.FromResult<XDocument>(new()))
                }
            );
        }
    }

    public static IModDiffer MakeMocker() => new ModDifferMocker();

    private ModDifferCLI Make(IFileIO fileio) => new(
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
        var mod = MakeValidInput(fileio, Path.Combine(modRoot, "filelist.xml"), root: new XElement("contentpackage"));
        var diff = MakeValidOutput(fileio, Path.Combine(diffRoot, FileNames.ModDiff));

        await tool.Apply(cpRoot, modRoot, diffRoot, true);

        ValidateInput(cp);
        ValidateInput(mod);
        ValidateOutput(diff);
    }
}
