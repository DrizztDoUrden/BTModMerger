using System.Xml.Linq;

using BTModMerger.Core.Interfaces;
using BTModMerger.LargeTools;
using BTModMerger.Tests.Mockers;

using static BTModMerger.Tests.CLI.CLITestHelpers;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Tests.CLI;

public class ContentPackageFuserCLI_Tests
{
    private class ContentPackageFuserMocker : IContentPackageFuser
    {
        public (Task<XDocument> manifest, IEnumerable<(string path, Task<XDocument> data)> files) Apply(XDocument contentPackage, Func<string, Task<XDocument>> fileGetters)
        {
            return (
                Task.FromResult<XDocument>(new(ContentPackage())),
                Array.Empty<(string, Task<XDocument>)>()
            );
        }
    }

    public static IContentPackageFuser MakeMocker() => new ContentPackageFuserMocker();

    private ContentPackageFuserCLI Make(IFileIO fileio) => new(
        fileio,
        MakeMocker()
    );

    [Fact]
    public void FindPackageRoot()
    {
        Assert.Throws<InvalidDataException>(() => ContentPackageFuserCLI.FindPackageRoot(null));
        Assert.Throws<InvalidDataException>(() => ContentPackageFuserCLI.FindPackageRoot(@"c:\"));
        Assert.Equal(new DirectoryInfo("Barotrauma").FullName, ContentPackageFuserCLI.FindPackageRoot(Path.Combine("Barotrauma", "Content", "ContentPackages", "Vanilla.xml")));
    }

    [Fact]
    public async Task ManifestFromCin()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, root: new XElement("ContentPackage",
            new XElement("item")
        ));

        var output = MakeValidOutput(fileio, Path.Combine("target", "BTMMContentPackage.xml"));
        var item = MakeValidInput(fileio, Path.Combine("target", "item.xml"));

        await tool.Apply(null, "target", "package");

        ValidateInput(input);
        ValidateOutput(output);
    }

    [Fact]
    public async Task ManifestFromFile()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, Path.Combine("Barotrauma", "Vanilla.xml"));
        var output = MakeValidOutput(fileio, Path.Combine("target", "BTMMContentPackage.xml"));

        await tool.Apply(input.Path, "target", null);

        ValidateInput(input);
        ValidateOutput(output);
    }
}
