using System.Xml.Serialization;

namespace BTModMerger;

public class BTMetadata
{
    public static string Path { get; set; }

    public string[] Indexed { get; set; } =
    {
    };

    public string[] Indexes { get; set; } =
    {
    };

    public string[] Tricky { get; set; } =
    {
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
            ret.Indexed = ret.Indexed.Select(s => s.ToLower()).ToArray();
            ret.Tricky = ret.Tricky.Select(s => s.ToLower()).ToArray();
            return ret;
        }

        using var file = File.OpenRead(Path);
        var data = (BTMetadata)serializer.Deserialize(file)!;
        data.Indexed = data.Indexed.Select(s => s.ToLower()).ToArray();
        data.Tricky = data.Tricky.Select(s => s.ToLower()).ToArray();
        return data;
    }
}
