using System.Xml.Linq;

using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Utils;
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
            await foreach (var _ in tool.Apply(new XDocument(), s => null!, 1)) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in tool.Apply(new XDocument(new XElement("e")), s => null!, 1)) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in tool.Apply(new XDocument(new XElement(Namespace + "ContentPackage")), s => null!, 1)) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            try
            {
                await foreach (var _ in tool.Apply(new XDocument(new XElement("ContentPackage", new XElement("items"))), s => new XDocument(), 1)) { }
            }
            catch (AggregateException ex)
            {
                Assert.Single(ex.InnerExceptions);
                Assert.IsType<ParallelExecutionException<IGrouping<XName, XElement>>>(ex.InnerException);
                throw ex.InnerException.InnerException!;
            }
        });
    }

    [Fact]
    public void Empty()
    {
        var tool = Make();

        Assert.Empty(tool.Apply(new XDocument(new XElement("ContentPackage")), s => null!, 1).ToBlockingEnumerable());
    }

    [Fact]
    public void Minimal()
    {
        var tool = Make();

        var doc = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items")),
                new XElement("items", new XAttribute("file", "items")),
                new XElement("jobs", new XAttribute("file", "jobs"))
            )
        );

        Assert.Collection(
            tool.Apply(doc, s => new XDocument(new XElement(s, new XElement(s[..^1]))), 1).ToBlockingEnumerable(),
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Collection(data.Root!.Elements(),
                    e => Assert.Equal("item", e.Name),
                    e => Assert.Equal("item", e.Name)
                );
            },
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("jobs.xml", path);
                Assert.Collection(data.Root!.Elements(),
                    e => Assert.Equal("job", e.Name)
                );
            });
    }

    [Fact]
    public void MoreComplex()
    {
        var tool = Make();

        var doc = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items")),
                new XElement("items", new XAttribute("file", "items")),
                new XElement("jobs", new XAttribute("file", "jobs"))
            )
        );

        Assert.Collection(
            tool.Apply(doc, s => new XDocument(new XElement("test", new XAttribute("a", 1), new XElement(s[..^1]))), 1).ToBlockingEnumerable(),
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("items.xml", path);
                Assert.Collection(data.Root!.Elements(),
                    e => Assert.Equal("test", e.Name),
                    e => Assert.Equal("test", e.Name)
                );
            },
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("jobs.xml", path);
                Assert.Collection(data.Root!.Elements(),
                    e => Assert.Equal("test", e.Name)
                );
            });
    }
}
