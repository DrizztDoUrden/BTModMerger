using System.Xml.Linq;

namespace BTModMerger.Core.Schema;

// Taken mostly from https://stackoverflow.com/a/13048775/6078677
public static class XElementComparator
{
    private static class Xsi
    {
        public static XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static XName schemaLocation = xsi + "schemaLocation";
        public static XName noNamespaceSchemaLocation = xsi + "noNamespaceSchemaLocation";
    }

    public static List<(XElement item, int count)> Deduplicate(this IEnumerable<XElement> items) => items.Select(i => (i, 1)).Deduplicate();

    public static List<(XElement item, int count)> Deduplicate(this IEnumerable<(XElement item, int count)> items)
    {
        var ret = new List<(XElement item, int count)>();
        var tmp = items.ToList();

        for (var outerIndex = 0; outerIndex < tmp.Count; ++outerIndex)
        {
            var count = tmp[outerIndex].count;

            for (var innerIndex = outerIndex + 1; innerIndex < tmp.Count; ++innerIndex)
                if (XNode.DeepEquals(tmp[outerIndex].item, tmp[innerIndex].item))
                {
                    count += tmp[innerIndex].count;
                    tmp.RemoveAt(innerIndex);
                    --innerIndex;
                }

            ret.Add((tmp[outerIndex].item, count));
        }

        return ret;
    }

    public static List<(XElement item, XContainer container, int count, XElement request)> Deduplicate(this IEnumerable<(XElement item, XContainer container, int count, XElement request)> items)
    {
        var ret = new List<(XElement item, XContainer container, int count, XElement request)>();
        var tmp = items.ToList();

        for (var outerIndex = 0; outerIndex < tmp.Count; ++outerIndex)
        {
            var count = tmp[outerIndex].count;

            for (var innerIndex = outerIndex + 1; innerIndex < tmp.Count; ++innerIndex)
                if (tmp[outerIndex].container == tmp[innerIndex].container &&
                    tmp[outerIndex].request.GetBTMMPath() == tmp[innerIndex].request.GetBTMMPath() &&
                    XNode.DeepEquals(tmp[outerIndex].item, tmp[innerIndex].item))
                {
                    count += tmp[innerIndex].count;
                    tmp.RemoveAt(innerIndex);
                    --innerIndex;
                }

            ret.Add((tmp[outerIndex].item, tmp[outerIndex].container, count, tmp[outerIndex].request));
        }

        return ret;
    }

    public static IEnumerable<XAttribute> NormalizeAttributes(XElement element/*, bool havePSVI = false*/)
    {
        return element.Attributes()
                .Where(a => !a.IsNamespaceDeclaration &&
                    a.Name != Xsi.schemaLocation &&
                    a.Name != Xsi.noNamespaceSchemaLocation)
                .OrderBy(a => a.Name.NamespaceName)
                .ThenBy(a => a.Name.LocalName)
                .Select(
                    a =>
                    {
                        // if (!havePSVI)
                        return a;

                        //var dt = a.GetSchemaInfo()!.SchemaType!.TypeCode;
                        //return dt switch
                        //{
                        //    XmlTypeCode.Boolean => new XAttribute(a.Name, (bool)a),
                        //    XmlTypeCode.DateTime => new XAttribute(a.Name, (DateTime)a),
                        //    XmlTypeCode.Decimal => new XAttribute(a.Name, (decimal)a),
                        //    XmlTypeCode.Double => new XAttribute(a.Name, (double)a),
                        //    XmlTypeCode.Float => new XAttribute(a.Name, (float)a),
                        //    XmlTypeCode.HexBinary or XmlTypeCode.Language => new XAttribute(a.Name, ((string)a).ToLower()),
                        //    _ => a,
                        //};
                    }
                );
    }

    public static XNode? NormalizeNode(XNode node/*, bool havePSVI = false*/)
    {
        return node switch
        {
            // trim comments and processing instructions from normalized tree
            XComment or XProcessingInstruction => null,
            XElement e => NormalizeElement(e/*, havePSVI*/),
            // Only thing left is XCData and XText, so clone them
            _ => node
        };
    }

    public static XElement NormalizeElement(XElement element/*, bool havePSVI = false*/)
    {
        //if (havePSVI)
        //{
        //    var dt = element.GetSchemaInfo();
        //    return dt!.SchemaType!.TypeCode switch
        //    {
        //        XmlTypeCode.Boolean => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                (bool)element),
        //        XmlTypeCode.DateTime => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                (DateTime)element),
        //        XmlTypeCode.Decimal => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                (decimal)element),
        //        XmlTypeCode.Double => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                (double)element),
        //        XmlTypeCode.Float => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                (float)element),
        //        XmlTypeCode.HexBinary or XmlTypeCode.Language => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                ((string)element).ToLower()),
        //        _ => new XElement(element.Name,
        //                                NormalizeAttributes(element, havePSVI),
        //                                element.Nodes().Select(n => NormalizeNode(n, havePSVI))
        //                            ),
        //    };
        //}

        return new XElement(element.Name,
            NormalizeAttributes(element/*, havePSVI*/),
            element.Nodes().Select(n => NormalizeNode(n/*, havePSVI*/))
        );
    }
}