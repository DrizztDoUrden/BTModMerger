﻿using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace BTModMerger.Tests;

using static BTMMSchema;
using static BTModMerger.CLI.CLI;

public class Simplifier_Tests
{
    private Simplifier Make(LoggerFactory lf) => new(
        new Logger<Simplifier>(lf),
        BTMetadata.Test,
        new Delinearizer(new Logger<Delinearizer>(lf))
    );

    [Fact]
    public void Empty()
    {
        using var lf = new LoggerFactory();
        var simplifier = Make(lf);

        var source = new XDocument(Diff());

        var result = simplifier.Apply(source, "", new(), null);

        Assert.Equal(source, result, XNode.DeepEquals);
    }

    public static IEnumerable<object[]> GetOptionsData()
    {
        foreach (var addNamespacePolicy in Enum.GetValues<AddNamespacePolicy>())
            foreach (var conflictHandlingPolicy in Enum.GetValues<ConflictHandlingPolicy>())
                yield return [new Simplifier.Options(addNamespacePolicy, conflictHandlingPolicy)];
    }

    private static XDocument Apply(AddNamespacePolicy policy, XDocument source)
    {
        var ret = new XDocument(source);
        if (policy == AddNamespacePolicy.Preserve) return ret;
        if (ret.Root is not null) Apply(policy, ret.Root);
        return ret;
    }

    private static void Apply(AddNamespacePolicy policy, XElement output)
    {
        if (output.Name == Elements.Diff || output.Name == Elements.Into)
        {
            foreach (var child in output.Elements())
                Apply(policy, child);
            return;
        }

        switch (policy)
        {
            case AddNamespacePolicy.Add:
                if (output.Name.Namespace == XNamespace.None)
                    output.Name = AddNamespace + output.Name.LocalName;
                break;
            case AddNamespacePolicy.Remove:
                if (output.Name.Namespace == AddNamespace)
                    output.Name = output.Name.LocalName;
                break;
        }
    }

    [Theory]
    [MemberData(nameof(GetOptionsData))]
    public void Simple(Simplifier.Options options)
    {
        using var lf = new LoggerFactory();
        var simplifier = Make(lf);

        var source = new XDocument(Diff(
            Into("root",
                Into("e0[@id0]",
                    new XElement("e1")
                ),
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("e0[@id1]"),
                    new XAttribute(AddNamespace + "e1", "1234"),
                    new XAttribute(RemoveNamespace + "e2", "")
                ),
                new XElement("e0",
                    new XAttribute("identifier", "id2"),
                    new XElement("e1")
                ),
                new XElement(AddNamespace + "e0",
                    new XAttribute("identifier", "id3"),
                    new XElement("e1")
                ),
                new XElement(RemoveNamespace + "e0",
                    new XAttribute("identifier", "id4"),
                    new XElement("e1")
                ),
                RemoveElement("e0[@id5]")
            )
        ));

        var expected = Apply(options.AddNamespacePolicy, source);

        var result = simplifier.Apply(source, "", options, null);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Theory]
    [MemberData(nameof(GetOptionsData))]
    public void RedundantInto(Simplifier.Options options)
    {
        using var lf = new LoggerFactory();
        var simplifier = Make(lf);

        var source = new XDocument(Diff(
            Into("root",
                Into("e0[@id0]",
                    Into("e1",
                        new XElement("e2")
                    )
                )
            )
        ));

        var expected = Apply(options.AddNamespacePolicy, new XDocument(Diff(
            Into("root/e0[@id0]/e1",
                new XElement("e2")
            )
        )));

        var result = simplifier.Apply(source, "", options, null);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }

    [Theory]
    [MemberData(nameof(GetOptionsData))]
    public void IntoToUpdateAttrs(Simplifier.Options options)
    {
        using var lf = new LoggerFactory();
        var simplifier = Make(lf);

        var source = new XDocument(Diff(
            Into("root",
                Into("e0[@id0]",
                    Into("e1",
                        new XAttribute(AddNamespace + "attr", "1234")
                    )
                ),
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("e0[@id1]/e1"),
                    new XAttribute(AddNamespace + "attr2", "678")
                ),
                Into("e0[@id1]",
                    Into("e1",
                        new XAttribute(AddNamespace + "attr", "1234")
                    )
                ),
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("e0[@id1]/e1"),
                    new XAttribute(RemoveNamespace + "attr3", "")
                )
            )
        ));

        var expected = Apply(options.AddNamespacePolicy, new XDocument(Diff(
            Into("root",
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("e0[@id1]/e1"),
                    new XAttribute(AddNamespace + "attr2", "678"),
                    new XAttribute(AddNamespace + "attr", "1234"),
                    new XAttribute(RemoveNamespace + "attr3", "")
                ),
                new XElement(Elements.UpdateAttributes,
                    PathAttribute("e0[@id0]/e1"),
                    new XAttribute(AddNamespace + "attr", "1234")
                )
            )
        )));

        var result = simplifier.Apply(source, "", options, null);

        Assert.Equal(expected, result, XNode.DeepEquals);
    }
}
