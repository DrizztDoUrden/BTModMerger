using System.Xml.Serialization;

namespace BTModMerger;

public class BTMetadata
{
    public static string Path { get; set; }

    public string[] Indexed { get; set; } =
    {
        "ItemSet",
        "sprite",
    };

    public string[] Tricky { get; set; } =
    {
        "Item",
    };

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

    private static readonly object _lock = new();
    private static BTMetadata? _singleton = null;

    private BTMetadata() {}

    private static BTMetadata Load()
    {
        var serializer = new XmlSerializer(typeof(BTMetadata));

        if (!File.Exists(Path))
        {
            using var blank = File.Create(Path);
            var ret = new BTMetadata();
            serializer.Serialize(blank, ret);
            return ret;
        }

        using var file = File.OpenRead(Path);
        return (BTMetadata)serializer.Deserialize(file)!;
    }
}
