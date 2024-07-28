using BTModMerger.Core.Interfaces;

namespace BTModMerger.Tools;

using static BTModMerger.Core.Schema.BTMMSchema;

public class DelinearizerCLI(
    IFileIO fileio,
    IDelinearizer delinearizer
)
{
    public void Apply(string? inputPath, string? outputPath, bool inPlace = false)
    {
        var input = fileio.OpenInput(ref inputPath);

        if (input.Root is null || input.Root.Name != Elements.Diff)
            throw new InvalidDataException($"({inputPath}) should be a BTMM diff xml.");

        var to = delinearizer.Apply(input, inputPath);
        fileio.SaveResult(inPlace ? inputPath : outputPath, to);
    }
}
