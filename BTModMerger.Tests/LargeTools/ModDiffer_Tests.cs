using System.Xml.Linq;

using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Schema;
using BTModMerger.Tests.CLI;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Tests.LargeTools;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

public class ModDiffer_Tests
{
    public static ModDiffer Make() => new(
        DifferCLI_Tests.MakeMocker()
    );

    [Fact]
    public async Task Throws()
    {
        var tool = Make();

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(), async s => null!,
                new XDocument(), async s => null!,
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("e")), async s => null!,
                new XDocument(), async s => null!,
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("ContentPackage")), async s => null!,
                new XDocument(), async s => null!,
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage()), async s => null!,
                new XDocument(new XElement("e")), async s => null!,
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage(new XElement("items"))), async s => new XDocument(),
                new XDocument(new XElement("ContentPackage", new XElement("items"))), async s => null!,
                [],
                alwaysOverride: false
            ).manifest;
        });
    }

    [Fact]
    public async Task Empty()
    {
        var tool = Make();

        var (manifest, files) = tool.Apply(
            new XDocument(ContentPackage()), async s => null!,
            new XDocument(new XElement("ContentPackage")), async s => null!,
            [],
            alwaysOverride: false
        );

        var expected = new XDocument(ModDiff());

        Assert.Empty(files);
        Assert.Equal(expected, await manifest, XNode.DeepEquals);
    }

    [Fact]
    public async Task Minimal()
    {
        var tool = Make();

        var doc = new XDocument(
            ContentPackage(
                new XElement("items", PathAttribute("items.xml")),
                new XElement("tests", PathAttribute("tests.xml")),
                new XElement("jobs", PathAttribute("jobs.xml"))
            )
        );

        var mod = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items")),
                new XElement("items", new XAttribute("file", "items")),
                new XElement("jobs", new XAttribute("file", "jobs"))
            )
        );

        var (manifestTask, files) = tool.Apply(
            doc, async s => new XDocument(new XElement(s)),
            mod, async s => new XDocument(new XElement(s, new XElement(s[..^1]))),
            ["missing.xml"],
            alwaysOverride: false
        );

        await Assert.CollectionAsync(
            files,
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items", path);
                Assert.Empty((await data).Root!.Elements());
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items", path);
                Assert.Empty((await data).Root!.Elements());
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("jobs", path);
                Assert.Empty((await data).Root!.Elements());
            });

        var manifest = await manifestTask;

        Assert.Equal(Elements.ModDiff, manifest.Root!.Name);

        Assert.Collection(
            manifest.Root.Elements().OrderBy(e => e.Name.LocalName),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(Copy("missing.xml")),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items"), BaseAttribute("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items"), BaseAttribute("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("jobs", PathAttribute("jobs"), BaseAttribute("jobs.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            )
        );
    }
}

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
