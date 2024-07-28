using static BTModMerger.Core.Schema.BTMMSchema;
using static BTModMerger.Core.ToolBase;
using System.Xml.Linq;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.Schema;

namespace BTModMerger.Core.Tools;

public class Delinearizer : IDelinearizer
{
    public XDocument Apply(XDocument input, string inputPath)
    {
        var to = new XDocument(Diff());
        var dbgPath = $"{inputPath}:Diff";
        Delinearize(input.Root!, to.Root!, dbgPath);
        TryMoveAttributesToIntos(to.Root!, "<temporary>");
        return to;
    }

    private void Delinearize(XElement input, XElement output, string dbgPath, string path = "")
    {
        dbgPath = CombineBTMMPaths(dbgPath, input.Name);

        if (input.Name == Elements.Diff)
        {
            foreach (var child in input.Elements())
                Delinearize(child, output, dbgPath);
            return;
        }

        var originalPath = input.GetBTMMPath();

        if (string.IsNullOrEmpty(originalPath))
        {
            output.Add(input);
            return;
        }

        originalPath = CombineBTMMPaths(path, originalPath);

        var parts = SplitPath(originalPath);
        var target = output;
        var tmpIntos = new List<XElement>();

        foreach (var part in parts)
        {
            var into = target.Elements(Elements.Into)
                .Where(e => e.GetBTMMPath() == part)
                .FirstOrDefault();

            if (into is null)
            {
                into = Into(part);
                target.Add(into);
                tmpIntos.Add(into);
            }

            target = into;
        }

        try
        {
            if (input.Name == Elements.Into)
            {
                foreach (var child in input.Elements())
                    Delinearize(child, output, dbgPath, originalPath);

                return;
            }

            var copy = new XElement(input);

            if (input.Name == Elements.RemoveElement ||
                input.Name == Elements.UpdateAttributes)
            {
                copy.SetAttributeValue(Attributes.Path, parts[^1]);
                target = target.Parent!;
            }
            else
                copy.Attribute(Attributes.Path)!.Remove();

            target.Add(copy);
        }
        finally
        {
            for (var i = tmpIntos.Count - 1; i >= 0 && !tmpIntos[i].HasElements; --i)
                tmpIntos[i].Remove();
        }
    }

    private void TryMoveAttributesToIntos(XElement from, string dbgPath)
    {
        var fromPath = from.GetBTMMPath();

        var dbgPathPart = from.Name.Fancify();
        if (!string.IsNullOrEmpty(fromPath)) dbgPathPart += $"({fromPath})";
        dbgPath += CombineBTMMPaths(dbgPath, dbgPathPart);

        if (from.Name == Elements.Diff || from.Name == Elements.Into)
        {
            foreach (var child in from.Elements().ToArray())
                TryMoveAttributesToIntos(child, dbgPath);
            return;
        }

        if (from.Name == Elements.UpdateAttributes)
        {
            var parent = from.Parent!;

            if (string.IsNullOrEmpty(fromPath))
                throw new InvalidDataException($"Empty path at {dbgPath}");

            var targets = parent.Elements(Elements.Into)
                .Where(e => e.GetBTMMPath() == fromPath)
                .ToArray();

            foreach (var attr in from.Attributes().ToArray())
            {
                if (attr.Name == Attributes.Path)
                    continue;

                var target = targets.FirstOrDefault(e => e.Attribute(attr.Name) is null);

                if (target is not null)
                {
                    target.SetAttributeValue(attr.Name, attr.Value);
                    attr.Remove();
                }
            }

            if (from.Attributes().Count() == 1)
                from.Remove();
            return;
        }
    }
}
