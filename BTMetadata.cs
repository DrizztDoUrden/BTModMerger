using System.Xml.Linq;
using System.Xml.Serialization;

namespace BTModMerger;

public class BTMetadata
{
    public static string Path { get; set; }

    public class IdMapping
    {
        public string Element { get; set; } = string.Empty;
        public string[] Ids { get; set; } = [];
    }

    public string[] Indexed { get; set; } = [];
    public string[] Indexes { get; set; } = [];
    public string[] Tricky { get; set; } = [];
    public string[] IndexByFilename { get; set; } = [];
    public string[] Partial { get; set; } = [];
    public IdMapping[] IdMappings { get; set; } = [];

    public static BTMetadata Instance
    {
        get
        {
            if (_singleton != null) return _singleton;
            lock (_lock)
            {
                if (_singleton != null) return _singleton;
                _singleton = Load();
                return _singleton;
            }
        }
    }

    public string? GetId(XElement element)
        => _idMappingFunctions.TryGetValue(element.Name.Fancify().ToLower(), out var func)
            ? func(element)
            : element.GetBTAttributeCIS("Identifier")!;

    private static readonly object _lock = new();
    private static BTMetadata? _singleton = null;

    [XmlIgnore]
    private Dictionary<string, Func<XElement, string?>> _idMappingFunctions = [];

    private BTMetadata() {}

    private static BTMetadata Load()
    {
        var serializer = new XmlSerializer(typeof(BTMetadata));

        if (!File.Exists(Path))
        {
            using var blank = File.Create(Path);
            var ret = new BTMetadata();
            ret.ToLower();
            serializer.Serialize(blank, ret);
            ret.GenerateMappings();
            return ret;
        }

        using var file = File.OpenRead(Path);
        var data = ((BTMetadata)serializer.Deserialize(file)!);
        data.ToLower();
        data.GenerateMappings();
        return data;
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
                            return parts.All(part => part is not null)
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
