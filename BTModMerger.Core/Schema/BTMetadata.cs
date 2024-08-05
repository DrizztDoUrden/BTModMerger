using System.Xml.Linq;
using System.Xml.Serialization;

namespace BTModMerger.Core.Schema;

public class BTMetadata
{
    public class IdMapping
    {
        public string Element { get; set; } = string.Empty;
        public HashSet<string> Ids { get; set; } = [];
    }

    public static readonly BTMetadata Test = new()
    {
        Indexed = ["indexed"],
        Indexes = ["indexes"],
        Tricky = ["tricky"],
        IndexByFilename = ["indexbyfilename"],
        Partial = ["partial"],
        RootContainers = ["rootcontainer"],
        OverrideNodes = ["override"],
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

    public HashSet<string> Indexed { get; set; } = [];
    public HashSet<string> Indexes { get; set; } = [];
    public HashSet<string> Tricky { get; set; } = [];
    public HashSet<string> IndexByFilename { get; set; } = [];
    public HashSet<string> Partial { get; set; } = [];
    public HashSet<string> RootContainers { get; set; } = [];
    public HashSet<string> OverrideNodes { get; set; } = [];
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
        Indexed = Indexed.Select(s => s.ToLower()).ToHashSet();
        Indexes = Indexes.Select(s => s.ToLower()).ToHashSet();
        Tricky = Tricky.Select(s => s.ToLower()).ToHashSet();
        IndexByFilename = IndexByFilename.Select(s => s.ToLower()).ToHashSet();
        Partial = Partial.Select(s => s.ToLower()).ToHashSet();
        RootContainers = RootContainers.Select(s => s.ToLower()).ToHashSet();
        OverrideNodes = OverrideNodes.Select(s => s.ToLower()).ToHashSet();
        IdMappings = IdMappings
            .Select(m
                => new IdMapping
                {
                    Element = m.Element.ToLower(),
                    Ids = m.Ids.Select(id => id.ToLower()).ToHashSet(),
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
                    var firstId = ids.First();

                    return new KeyValuePair<string, Func<XElement, string?>>(
                        mapping.Element,
                        ids.Count > 1
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
