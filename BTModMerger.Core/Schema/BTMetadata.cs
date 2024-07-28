using System.Xml.Linq;
using System.Xml.Serialization;

namespace BTModMerger.Core.Schema;

public class BTMetadata
{
    public class IdMapping
    {
        public string Element { get; set; } = string.Empty;
        public string[] Ids { get; set; } = [];
    }

    public static readonly BTMetadata Test = new()
    {
        Indexed = ["indexed"],
        Indexes = ["indexes"],
        Tricky = ["tricky"],
        IndexByFilename = ["indexbyfilename"],
        Partial = ["partial"],
        IdMappings = [
            new()
            {
                Element = "idmapped",
                Ids = [
                    "id0",
                    "id1",
                ],
            },
        ],
    };

    public string[] Indexed { get; set; } = [];
    public string[] Indexes { get; set; } = [];
    public string[] Tricky { get; set; } = [];
    public string[] IndexByFilename { get; set; } = [];
    public string[] Partial { get; set; } = [];
    public IdMapping[] IdMappings { get; set; } = [];

    public string? GetId(XElement element)
        => _idMappingFunctions.TryGetValue(element.Name.Fancify().ToLower(), out var func)
            ? func(element)
            : element.GetBTAttributeCIS("Identifier")!;

    public static BTMetadata Load(XDocument document)
    {
        var serializer = new XmlSerializer(typeof(BTMetadata));
        using var reader = document.CreateReader();

        var data = (BTMetadata)serializer.Deserialize(reader)!;
        data.Prepare();
        return data;
    }

    public static BTMetadata Load(string path)
    {
        var serializer = new XmlSerializer(typeof(BTMetadata));

        if (!File.Exists(path))
        {
            using var blank = File.Create(path);
            var ret = new BTMetadata();
            serializer.Serialize(blank, ret);
            ret.Prepare();
            return ret;
        }

        using var file = File.OpenRead(path);
        var data = (BTMetadata)serializer.Deserialize(file)!;
        data.Prepare();
        return data;
    }

    [XmlIgnore]
    private Dictionary<string, Func<XElement, string?>> _idMappingFunctions = [];

    private BTMetadata() { }

    private void Prepare()
    {
        ToLower();
        GenerateMappings();
    }

    private void ToLower()
    {
        Indexed = Indexed.Select(s => s.ToLower()).ToArray();
        Indexes = Indexes.Select(s => s.ToLower()).ToArray();
        Tricky = Tricky.Select(s => s.ToLower()).ToArray();
        IndexByFilename = IndexByFilename.Select(s => s.ToLower()).ToArray();
        Partial = Partial.Select(s => s.ToLower()).ToArray();
        IdMappings = IdMappings
            .Select(m
                => new IdMapping
                {
                    Element = m.Element.ToLower(),
                    Ids = m.Ids.Select(id => id.ToLower()).ToArray(),
                }
            )
            .ToArray();
    }

    private void GenerateMappings()
    {
        _idMappingFunctions = IdMappings
            .Select(
                mapping =>
                {
                    var ids = mapping.Ids;
                    var firstId = ids[0];

                    return new KeyValuePair<string, Func<XElement, string?>>(
                        mapping.Element,
                        ids.Length > 1
                        ? e =>
                        {
                            var parts = ids.Select(id => e.GetBTAttributeCIS(id)).ToArray();
                            return parts.Any(part => part is not null)
                                ? string.Join(":", parts.Select(part => part ?? "btmm~~none"))
                                : null;
                        }
                    : e => e.GetBTAttributeCIS(firstId)
                    );
                }
            )
            .ToDictionary();
    }
}
