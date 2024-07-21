using static BTModMerger.BTMMSchema;
using static BTModMerger.ToolBase;
using System.Xml.Linq;

namespace BTModMerger;

static internal class Linearizer
{
    public static void Apply(string? inputPath, string? outputPath)
    {
        var baseFile = string.IsNullOrWhiteSpace(inputPath)
            ? Console.OpenStandardInput()
            : File.OpenRead(inputPath);

        inputPath ??= "cin";
        var input = XDocument.Load(baseFile, LoadOptions.None);

        if (baseFile is FileStream)
            baseFile.Dispose();

        if (input.Root is null || input.Root.Name != Elements.Diff)
        {
            Log.Error($"({inputPath}) should be a BTMM diff xml.");
            return;
        }

        var to = Apply(input, inputPath);
        SaveResult(outputPath, to);
    }

    public static XDocument Apply(XDocument input, string inputPath)
    {
        var to = new XDocument(Diff());
        Linearize(input.Root!, to.Root!, "", inputPath);
        return to;
    }

    private static void Linearize(XElement input, XElement output, string path, string dbgPath)
    {
        if (input.Name == Elements.Diff)
        {
            foreach (var child in input.Elements())
                Linearize(child, output, path, CombineBTMMPaths(dbgPath, Elements.Diff));
            return;
        }

        if (input.Name == Elements.Into)
        {
            var intoPath = input.GetBTMMPath();

            if (string.IsNullOrEmpty(intoPath))
                throw new InvalidDataException($"btmm:Into element with missing btmm:Path attribute at {dbgPath}");

            path = CombineBTMMPaths(path, intoPath);

            foreach (var child in input.Elements())
                Linearize(child, output, path, CombineBTMMPaths(dbgPath, $"{Elements.Into}({path})"));

            XElement? updateAttributes = null;

            foreach (var attribute in input.Attributes())
            {
                if (attribute.Name == Attributes.Path)
                    continue;

                updateAttributes ??= output.Elements(Elements.UpdateAttributes)
                    .Where(existing => existing.GetBTMMPath() == path)
                    .FirstOrDefault();

                if (updateAttributes is null)
                {
                    updateAttributes = UpdateAttributes();
                    updateAttributes.SetAttributeValue(Attributes.Path, path);
                    output.Add(updateAttributes);
                }

                updateAttributes.Add(attribute);
            }
            return;
        }

        var copy = new XElement(input);

        if (path.Length > 0)
        {
            var attrPath = input.GetBTMMPath();
            attrPath = attrPath is null ? path : CombineBTMMPaths(path, attrPath);

            copy.SetAttributeSorting(Attributes.Path, attrPath);
        }

        output.Add(copy);
    }
}
