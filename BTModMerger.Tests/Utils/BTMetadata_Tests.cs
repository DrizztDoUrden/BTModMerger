using System.Xml.Linq;
using BTModMerger.Core.Schema;

namespace BTModMerger.Tests.Utils;

public class BTMetadata_Tests
{
    private static XElement[] IdMappingTestData =
    [
        new("Element0_Test",
            new XAttribute("Attribute0_Test", "Attribute0_Test_value"),
            new XAttribute("Attribute1_Test", "Attribute1_Test_value"),
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element0_Test",
            new XAttribute("Attribute1_Test", "Attribute1_Test_value"),
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element0_Test",
            new XAttribute("Attribute0_Test", "Attribute0_Test_value"),
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element0_Test",
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element0_Test"),
        new("Element1_Test",
            new XAttribute("Attribute0_Test", "Attribute0_Test_value"),
            new XAttribute("Attribute1_Test", "Attribute1_Test_value"),
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element1_Test",
            new XAttribute("Attribute0_Test", "Attribute0_Test_value"),
            new XAttribute("Attribute1_Test", "Attribute1_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element2_Test",
            new XAttribute("Attribute0_Test", "Attribute0_Test_value"),
            new XAttribute("Attribute1_Test", "Attribute1_Test_value"),
            new XAttribute("Attribute2_Test", "Attribute2_Test_value"),
            new XAttribute("Attribute3_Test", "Attribute3_Test_value")
        ),
        new("Element3_Test")
    ];

    private static readonly XDocument RawTestMetadata = new(
        new XElement(nameof(BTMetadata),
            new XElement(nameof(BTMetadata.Indexed),
                new XElement("string", "Indexed_TestValue1"),
                new XElement("string", "Indexed_TestValue2"),
                new XElement("string", "Indexed_TestValue3")
            ),
            new XElement(nameof(BTMetadata.Indexes),
                new XElement("string", "Indexes_TestValue1"),
                new XElement("string", "Indexes_TestValue2"),
                new XElement("string", "Indexes_TestValue3")
            ),
            new XElement(nameof(BTMetadata.Tricky),
                new XElement("string", "Tricky_TestValue1"),
                new XElement("string", "Tricky_TestValue2"),
                new XElement("string", "Tricky_TestValue3")
            ),
            new XElement(nameof(BTMetadata.IndexByFilename),
                new XElement("string", "IndexByFilename_TestValue1"),
                new XElement("string", "IndexByFilename_TestValue2"),
                new XElement("string", "IndexByFilename_TestValue3")
            ),
            new XElement(nameof(BTMetadata.Partial),
                new XElement("string", "Partial_TestValue1"),
                new XElement("string", "Partial_TestValue2"),
                new XElement("string", "Partial_TestValue3")
            ),
            new XElement(nameof(BTMetadata.IdMappings),
                new XElement(nameof(BTMetadata.IdMapping),
                    new XElement(nameof(BTMetadata.IdMapping.Element), "Element0_Test"),
                    new XElement(nameof(BTMetadata.IdMapping.Ids),
                        new XElement("string", "Attribute0_Test"),
                        new XElement("string", "Attribute1_Test")
                    )
                ),
                new XElement(nameof(BTMetadata.IdMapping),
                    new XElement(nameof(BTMetadata.IdMapping.Element), "Element1_Test"),
                    new XElement(nameof(BTMetadata.IdMapping.Ids),
                        new XElement("string", "Attribute2_Test")
                    )
                )
            )
        )
    );

    private static readonly BTMetadata TestMetadata = BTMetadata.Load(RawTestMetadata);

    private static XElement GetTestElement(XContainer container, params XName[] names)
    {
        foreach (var name in names)
        {
            var tmp = container.Element(name)!;
            Assert.NotNull(tmp);
            container = tmp;
        }
        Assert.IsType<XElement>(container);
        return (XElement)container;
    }

    private static void TestStringArrayField(string[] parsed, XElement property)
    {
        Assert.Equal(GetTestElement(property).Elements().Count(), parsed.Length);
        for (var i = 0; i < parsed.Length; ++i)
        {
            var item = property.Elements("string").Skip(i).FirstOrDefault();
            Assert.NotNull(item);
            Assert.Equal(item.Value, parsed[i], true);
        }
    }

    private static void TestStringArrayField(string[] parsed, XContainer container, XName name)
    {
        var property = GetTestElement(container, nameof(BTMetadata), name);
        TestStringArrayField(parsed, property);
    }

    private static string? MakeMappedValue(XElement element, BTMetadata meatadata)
    {
        var mapping = meatadata.IdMappings.FirstOrDefault(m => m.Element.Equals(element.Name.LocalName, StringComparison.InvariantCultureIgnoreCase));

        if (mapping is null)
            return null;

        var attrValues = mapping.Ids
            .Select(id => element.GetBTAttributeCIS(id))
            .ToArray();

        return attrValues.All(s => string.IsNullOrWhiteSpace(s))
            ? null
            : string.Join(":", attrValues.Select(s => s ?? "btmm~~none"));
    }

    private void Indexed_Parsed_Impl(BTMetadata data, XContainer xmlData) => TestStringArrayField(data.Indexed, xmlData, nameof(BTMetadata.Indexed));
    private void Indexes_Parsed_Impl(BTMetadata data, XContainer xmlData) => TestStringArrayField(data.Indexes, xmlData, nameof(BTMetadata.Indexes));
    private void Tricky_Parsed_Impl(BTMetadata data, XContainer xmlData) => TestStringArrayField(data.Tricky, xmlData, nameof(BTMetadata.Tricky));
    private void IndexByFilename_Parsed_Impl(BTMetadata data, XContainer xmlData) => TestStringArrayField(data.IndexByFilename, xmlData, nameof(BTMetadata.IndexByFilename));
    private void Partial_Parsed_Impl(BTMetadata data, XContainer xmlData) => TestStringArrayField(data.Partial, xmlData, nameof(BTMetadata.Partial));

    private void IdMappings_Parsed_Impl(BTMetadata data, XContainer xmlData)
    {
        var property = GetTestElement(xmlData, nameof(BTMetadata), nameof(BTMetadata.IdMappings));
        Assert.Equal(property.Elements().Count(), data.IdMappings.Length);

        for (var i = 0; i < data.IdMappings.Length; ++i)
        {
            var mapping = property.Elements(nameof(BTMetadata.IdMapping)).Skip(i).FirstOrDefault();
            Assert.NotNull(mapping);
            Assert.Equal(GetTestElement(mapping, nameof(BTMetadata.IdMapping.Element)).Value, data.IdMappings[i].Element, true);
            TestStringArrayField(data.IdMappings[i].Ids, GetTestElement(mapping, nameof(BTMetadata.IdMapping.Ids)));
        }
    }

    [Fact]
    public void Indexed_Parsed() => Indexed_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void Indexes_Parsed() => Indexes_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void Tricky_Parsed() => Tricky_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void IndexByFilename_Parsed() => IndexByFilename_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void Partial_Parsed() => Partial_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void IdMappings_Parsed() => IdMappings_Parsed_Impl(TestMetadata, RawTestMetadata);

    [Fact]
    public void IdMappings()
    {
        Assert.Null(TestMetadata.GetId(new XElement("Miss")));

        for (var i = 0; i < IdMappingTestData.Length; ++i)
        {
            var testElement = IdMappingTestData[i];
            Assert.Equal(MakeMappedValue(testElement, TestMetadata), TestMetadata.GetId(testElement));
        }
    }

    [Fact]
    public void LoadMissing()
    {
        var path = Path.GetTempFileName();
        File.Delete(path);
        BTMetadata.Load(path);
    }

    [Fact]
    public void LoadExisting()
    {
        var path = Path.GetTempFileName();
        RawTestMetadata.Save(path);

        var loaded = BTMetadata.Load(path);

        Indexed_Parsed_Impl(loaded, RawTestMetadata);
        Indexes_Parsed_Impl(loaded, RawTestMetadata);
        Tricky_Parsed_Impl(loaded, RawTestMetadata);
        IndexByFilename_Parsed_Impl(loaded, RawTestMetadata);
        Partial_Parsed_Impl(loaded, RawTestMetadata);
        IdMappings_Parsed_Impl(loaded, RawTestMetadata);
    }
}