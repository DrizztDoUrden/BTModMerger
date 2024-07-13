using System.Xml.Linq;
using System.Xml.Schema;

// Taken mostly from https://stackoverflow.com/a/13048775/6078677
static class XElementComparator
{
    private static class Xsi
    {
        public static XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static XName schemaLocation = xsi + "schemaLocation";
        public static XName noNamespaceSchemaLocation = xsi + "noNamespaceSchemaLocation";
    }

    public static List<(XElement item, int count)> Deduplicate(this IEnumerable<XElement> items)
    {
        var ret = new List<(XElement item, int count)>();
        var tmp = items.ToList();

        for (var outerIndex = 0; outerIndex < tmp.Count; ++outerIndex)
        {
            var count = 1;

            for (var innerIndex = outerIndex + 1; innerIndex < tmp.Count; ++innerIndex)
            {
                if (XNode.DeepEquals(tmp[outerIndex], tmp[innerIndex]))
                {
                    tmp.RemoveAt(innerIndex);
                    --innerIndex;
                    ++count;
                }
            }

            ret.Add((tmp[outerIndex], count));
        }

        return ret;
    }

    public static XDocument Normalize(XDocument source, XmlSchemaSet? schema)
    {
        var havePSVI = false;
        // validate, throw errors, add PSVI information
        if (schema != null)
        {
            source.Validate(schema, null, true);
            havePSVI = true;
        }
        return new XDocument(
            source.Declaration,
            source.Nodes().Select(n =>
            {
                // Remove comments, processing instructions, and text nodes that are
                // children of XDocument.  Only white space text nodes are allowed as
                // children of a document, so we can remove all text nodes.
                if (n is XComment || n is XProcessingInstruction || n is XText)
                    return null;
                var e = n as XElement;
                if (e != null)
                    return NormalizeElement(e, havePSVI);
                return n;
            }
            )
        );
    }

    public static bool DeepEqualsWithNormalization(XDocument doc1, XDocument doc2, XmlSchemaSet? schemaSet = null)
    {
        var d1 = Normalize(doc1, schemaSet);
        var d2 = Normalize(doc2, schemaSet);
        return XNode.DeepEquals(d1, d2);
    }

    public static bool DeepEqualsWithNormalization(XElement e1, XElement e2, XmlSchemaSet? schemaSet = null)
    {
        var d1 = NormalizeElement(e1);
        var d2 = NormalizeElement(e2);
        return XNode.DeepEquals(d1, d2);
    }

    public static IEnumerable<XAttribute> NormalizeAttributes(XElement element, bool havePSVI = false)
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
                        if (havePSVI)
                        {
                            var dt = a.GetSchemaInfo()!.SchemaType!.TypeCode;
                            switch (dt)
                            {
                                case XmlTypeCode.Boolean:
                                    return new XAttribute(a.Name, (bool)a);
                                case XmlTypeCode.DateTime:
                                    return new XAttribute(a.Name, (DateTime)a);
                                case XmlTypeCode.Decimal:
                                    return new XAttribute(a.Name, (decimal)a);
                                case XmlTypeCode.Double:
                                    return new XAttribute(a.Name, (double)a);
                                case XmlTypeCode.Float:
                                    return new XAttribute(a.Name, (float)a);
                                case XmlTypeCode.HexBinary:
                                case XmlTypeCode.Language:
                                    return new XAttribute(a.Name,
                                        ((string)a).ToLower());
                            }
                        }
                        return a;
                    }
                );
    }

    public static XNode? NormalizeNode(XNode node, bool havePSVI = false)
    {
        // trim comments and processing instructions from normalized tree
        if (node is XComment || node is XProcessingInstruction)
            return null;
        var e = node as XElement;
        if (e != null)
            return NormalizeElement(e, havePSVI);
        // Only thing left is XCData and XText, so clone them
        return node;
    }

    public static XElement NormalizeElement(XElement element, bool havePSVI = false)
    {
        if (havePSVI)
        {
            var dt = element.GetSchemaInfo();
            switch (dt!.SchemaType!.TypeCode)
            {
                case XmlTypeCode.Boolean:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        (bool)element);
                case XmlTypeCode.DateTime:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        (DateTime)element);
                case XmlTypeCode.Decimal:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        (decimal)element);
                case XmlTypeCode.Double:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        (double)element);
                case XmlTypeCode.Float:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        (float)element);
                case XmlTypeCode.HexBinary:
                case XmlTypeCode.Language:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        ((string)element).ToLower());
                default:
                    return new XElement(element.Name,
                        NormalizeAttributes(element, havePSVI),
                        element.Nodes().Select(n => NormalizeNode(n, havePSVI))
                    );
            }
        }
        else
        {
            return new XElement(element.Name,
                NormalizeAttributes(element, havePSVI),
                element.Nodes().Select(n => NormalizeNode(n, havePSVI))
            );
        }
    }
}