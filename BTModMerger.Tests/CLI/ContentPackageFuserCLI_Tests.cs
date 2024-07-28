using System.Xml.Linq;

using BTModMerger.Core;
using BTModMerger.Core.Interfaces;
using BTModMerger.LargeTools;
using BTModMerger.Tests.Mockers;

using static BTModMerger.Tests.CLI.CLITestHelpers;

namespace BTModMerger.Tests.CLI;

public class ContentPackageFuserCLI_Tests
{
    private class ContentPackageFuserMocker : IContentPackageFuser
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<(string path, XName kind, XDocument data)> Apply(XDocument contentPackage, Func<string, XDocument> fileGetters, int threads)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var child in contentPackage.Root!.Elements())
                yield return ($"{child.Name.Fancify()}.xml", child.Name, new XDocument(new XElement("e")));
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
        Assert.Equal(@"C:\Barotrauma", ContentPackageFuserCLI.FindPackageRoot(@"C:\Barotrauma\Content\ContentPackages\Vanilla.xml"));
    }

    [Fact]
    public async Task ManifestFromCin()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        MakeValidInput(fileio, root: new XElement("ContentPackage",
            new XElement("item")
        ));

        var output = MakeValidOutput(fileio, @"target\BTMMContentPackage.xml");
        var item = MakeValidInput(fileio, @"target\item.xml");

        await tool.Apply(null, "target", 1, "package");

        ValidateOutput(output);
    }

    [Fact]
    public async Task ManifestFromFile()
    {
        using var fileio = new FileIOMocker();
        var tool = Make(fileio);

        var input = MakeValidInput(fileio, @"c:\Barotrauma\Vanilla.xml");
        var output = MakeValidOutput(fileio, @"target\BTMMContentPackage.xml");

        await tool.Apply(input.Path, "target", 1, null);

        ValidateInput(input);
        ValidateOutput(output);
    }
}
