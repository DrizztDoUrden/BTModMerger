using System.Xml.Linq;

using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Utils;
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
            await foreach (var _ in tool.Apply(
                new XDocument(), s => null!,
                new XDocument(), s => null!,
                threads: 1,
                alwaysOverride: false
            )) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in tool.Apply(
                new XDocument(new XElement("e")), s => null!,
                new XDocument(), s => null!,
                threads: 1,
                alwaysOverride: false
            )) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in tool.Apply(
                new XDocument(new XElement("ContentPackage")), s => null!,
                new XDocument(), s => null!,
                threads: 1,
                alwaysOverride: false
            )) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in tool.Apply(
                new XDocument(ContentPackage()), s => null!,
                new XDocument(new XElement("e")), s => null!,
                threads: 1,
                alwaysOverride: false
            )) { }
        });

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            try
            {
                await foreach (var _ in tool.Apply(
                    new XDocument(ContentPackage(new XElement("items"))), s => new XDocument(),
                    new XDocument(new XElement("ContentPackage", new XElement("items"))), s => null!,
                    threads: 1,
                    alwaysOverride: false
                )) { }
            }
            catch (AggregateException ex)
            {
                Assert.Single(ex.InnerExceptions);
                Assert.IsType<ParallelExecutionException<XElement>>(ex.InnerException);
                throw ex.InnerException.InnerException!;
            }
        });
    }

    [Fact]
    public void Empty()
    {
        var tool = Make();

        Assert.Empty(tool.Apply(
            new XDocument(ContentPackage()), s => null!,
            new XDocument(new XElement("ContentPackage")), s => null!,
            threads: 1,
            alwaysOverride: false
        ).ToBlockingEnumerable());
    }

    [Fact]
    public void Minimal()
    {
        var tool = Make();

        var doc = new XDocument(
            ContentPackage(
                new XElement("items", PathAttribute("items")),
                new XElement("tests", PathAttribute("tests")),
                new XElement("jobs", PathAttribute("jobs"))
            )
        );

        var mod = new XDocument(
            new XElement("ContentPackage",
                new XElement("items", new XAttribute("file", "items")),
                new XElement("items", new XAttribute("file", "items")),
                new XElement("jobs", new XAttribute("file", "jobs"))
            )
        );

        Assert.Collection(
            tool.Apply(
                doc, s => new XDocument(new XElement(s)),
                mod, s => new XDocument(new XElement(s, new XElement(s[..^1]))),
                threads: 1,
                alwaysOverride: false
            ).ToBlockingEnumerable(),
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("items", path);
                Assert.Empty(data.Root!.Elements());
            },
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("items", path);
                Assert.Empty(data.Root!.Elements());
            },
            result =>
            {
                var (path, _, data) = result;
                Assert.Equal("jobs", path);
                Assert.Empty(data.Root!.Elements());
            });
    }
}
