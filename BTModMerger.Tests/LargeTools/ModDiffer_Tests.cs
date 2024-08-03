using System.Xml.Linq;

using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Schema;
using BTModMerger.Tests.CLI;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Tests.LargeTools;

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
                new XDocument(), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException(),
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("e")), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException(),
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("ContentPackage")), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException(),
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage()), s => throw new InvalidOperationException(),
                new XDocument(new XElement("e")), s => throw new InvalidOperationException(),
                [],
                alwaysOverride: false
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage(new XElement("items"))), s => Task.FromResult<XDocument>(new()),
                new XDocument(new XElement("ContentPackage", new XElement("items"))), s => throw new InvalidOperationException(),
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
            new XDocument(ContentPackage()), s => throw new InvalidOperationException(),
            new XDocument(new XElement("ContentPackage")), s => throw new InvalidOperationException(),
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
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("jobs", new XAttribute("file", "jobs.xml"))
            )
        );

        var (manifestTask, files) = tool.Apply(
            doc, s => Task.FromResult<XDocument>(new(new XElement(s))),
            mod, s => Task.FromResult<XDocument>(new(new XElement(s, new XElement(s[..^5])))),
            ["missing.xml"],
            alwaysOverride: false
        );

        await Assert.CollectionAsync(
            files,
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Empty((await data).Root!.Elements());
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Empty((await data).Root!.Elements());
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("jobs.xml", path);
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
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items.xml"), BaseAttribute("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items.xml"), BaseAttribute("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("jobs", PathAttribute("jobs.xml"), BaseAttribute("jobs.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            )
        );
    }
}
