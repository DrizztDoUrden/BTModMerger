using System.Xml.Linq;

using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Schema;
using BTModMerger.Tests.Tools;

using static BTModMerger.Core.Schema.BTMMSchema;

namespace BTModMerger.Tests.LargeTools;

public class ContentPackageFuser_Tests
{
    public static ContentPackageFuser Make() => new(
        Fuser_Tests.Make()
    );

    [Fact]
    public async Task Throws()
    {
        var tool = Make();

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(new XDocument(), s => Task.FromResult<XDocument>(null!)).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(new XDocument(new XElement("e")), s => Task.FromResult<XDocument>(null!)).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await tool.Apply(new XDocument(new XElement(Namespace + "ContentPackage")), s => Task.FromResult<XDocument>(null!)).manifest;
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            var (manifest, files) = tool.Apply(
                new XDocument(new XElement("ContentPackage", new XElement("items"))),
                s => Task.FromResult<XDocument>(new())
            );

            foreach (var (path, task) in files)
                await task;

            await manifest;
        });
    }

    [Fact]
    public async Task Empty()
    {
        var tool = Make();
        var (manifest, files) = tool.Apply(new XDocument(new XElement("ContentPackage")), s => Task.FromResult<XDocument>(null!));

        var expected = new XDocument(ContentPackage());

        Assert.Empty(files);
        Assert.Equal(await manifest, expected, XNode.DeepEquals);
    }

    [Fact]
    public async Task Minimal()
    {
        var tool = Make();

        var doc = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("jobs", new XAttribute("file", "jobs.xml"))
            )
        );

        var (manifestTask, files) = tool.Apply(
            doc,
            s => Task.FromResult<XDocument>(new(new XElement(s[..^4], new XElement(s[..^5]))))
        );

        await Assert.CollectionAsync(
            files,
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Collection((await data).Root!.Elements(),
                    e => Assert.Equal("item", e.Name),
                    e => Assert.Equal("item", e.Name)
                );
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("jobs.xml", path);
                Assert.Collection((await data).Root!.Elements(),
                    e => Assert.Equal("job", e.Name)
                );
            });

        var manifest = await manifestTask;

        Assert.Equal(Elements.ContentPackage, manifest.Root!.Name);

        Assert.Collection(
            manifest.Root.Elements().OrderBy(e => e.Name.LocalName),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items.xml"), Part("items.xml"), Part("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("jobs", PathAttribute("jobs.xml"), Part("jobs.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            )
        );
    }

    [Fact]
    public async Task MoreComplex()
    {
        var tool = Make();

        var doc = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("items", new XAttribute("file", "items.xml")),
                new XElement("jobs", new XAttribute("file", "jobs.xml"))
            )
        );

        var (manifestTask, files) = tool.Apply(
            doc,
            s => Task.FromResult<XDocument>(new(new XElement("test", new XAttribute("a", 1), new XElement(s[..^1]))))
        );

        await Assert.CollectionAsync(
            files,
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Collection((await data).Root!.Elements(),
                    e => Assert.Equal("test", e.Name),
                    e => Assert.Equal("test", e.Name)
                );
            },
            async result =>
            {
                var (path, data) = result;
                Assert.Equal("jobs.xml", path);
                Assert.Collection((await data).Root!.Elements(),
                    e => Assert.Equal("test", e.Name)
                );
            });

        var manifest = await manifestTask;

        Assert.Equal(Elements.ContentPackage, manifest.Root!.Name);

        Assert.Collection(
            manifest.Root.Elements().OrderBy(e => e.Name.LocalName),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("items", PathAttribute("items.xml"), Part("items.xml"), Part("items.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            ),
            element => Assert.Equal(
                XElementComparator.NormalizeElement(new XElement("jobs", PathAttribute("jobs.xml"), Part("jobs.xml"))),
                XElementComparator.NormalizeElement(element),
                XNode.DeepEquals
            )
        );
    }
}
