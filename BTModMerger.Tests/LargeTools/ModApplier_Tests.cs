using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Schema;
using BTModMerger.Tests.CLI;
using static BTModMerger.Core.Schema.BTMMSchema;
using System.Xml.Linq;

namespace BTModMerger.Tests.LargeTools;

public class ModApplier_Tests
{
    public static ModApplier Make() => new(
        ApplierCLI_Tests.MakeMocker()
    );

    [Fact]
    public async Task Throws()
    {
        var tool = Make();

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException()
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("e")), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException()
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(new XElement("ContentPackage")), s => throw new InvalidOperationException(),
                new XDocument(), s => throw new InvalidOperationException()
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage()), s => throw new InvalidOperationException(),
                new XDocument(new XElement("e")), s => throw new InvalidOperationException()
            ).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(
                new XDocument(ContentPackage(new XElement("items"))), s => Task.FromResult<XDocument>(new()),
                new XDocument(new XElement("ModDiff", new XElement("items"))), s => throw new InvalidOperationException()
            ).manifest;
        });
    }

    [Fact]
    public async Task Empty()
    {
        var tool = Make();

        var (manifest, files, copies) = tool.Apply(
            new XDocument(ContentPackage()), s => throw new InvalidOperationException(),
            new XDocument(ModDiff()), s => throw new InvalidOperationException()
        );

        var expected = new XDocument(new XElement("ContentPackage"));

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
            ModDiff(
                new XElement("items", PathAttribute("items")),
                new XElement("items", PathAttribute("items")),
                new XElement("jobs", PathAttribute("jobs")),
                Copy("copy.xml")
            )
        );

        var (manifestTask, files, copies) = tool.Apply(
            doc, s => Task.FromResult<XDocument>(new(new XElement(s))),
            mod, s => Task.FromResult<XDocument>(new(new XElement(s, new XElement(s[..^1]))))
        );

        await Assert.CollectionAsync(
            files,
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items", path);
                Assert.Null((await data).Root); // mocker works this way
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items", path);
                Assert.Null((await data).Root); // mocker works this way
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("jobs", path);
                Assert.Null((await data).Root); // mocker works this way
            });

        var manifest = await manifestTask;

        Assert.Equal("ContentPackage", manifest.Root!.Name);

        Assert.Collection(
            manifest.Root.Elements().OrderBy(e => e.Name.LocalName),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", new XAttribute("file", "%ModDir%/items"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", new XAttribute("file", "%ModDir%/items"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("jobs", new XAttribute("file", "%ModDir%/jobs"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            )
        );

        Assert.Collection(copies,
            s => Assert.Equal("copy.xml", s)
        );
    }
}
