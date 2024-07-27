using System.Xml.Linq;

using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.BTMMSchema;
using static BTModMerger.Core.ToolBase;

namespace BTModMerger.Core;

public sealed class Fuser(
    BTMetadata metadata
)
    : IFuser
{
    public void Apply(XElement to, XElement part, string dbgPath, string filename)
    {
        var nextPath = CombineBTMMPaths(dbgPath, part.Name);

        if (part.Name == Elements.Diff || part.Name == Elements.FusedBase)
        {
            foreach (var child in part.Elements())
                Apply(to, child, nextPath, filename);
            return;
        }

        if (to.Name == Elements.FusedBase)
            if (metadata.IndexByFilename.Contains(part.Name.Fancify().ToLower()))
            {
                var copy = new XElement(part);
                if (copy.Attribute(Attributes.File) is null)
                    copy.SetAttributeValue(Attributes.File, filename);
                to.Add(copy);
                return;
            }

        if (metadata.Partial.Contains(part.Name.Fancify().ToLower()))
        {
            var target = to.Elements(part.Name)
                .SingleOrDefault();

            if (target is null)
                to.Add(new XElement(part));
            else
                foreach (var item in part.Elements())
                    target.Add(item);

            return;
        }

        to.Add(part);
    }
}
