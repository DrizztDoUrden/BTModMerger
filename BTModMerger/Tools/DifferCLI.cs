using BTModMerger.Core.Interfaces;

namespace BTModMerger.Tools;

public class DifferCLI(
    IFileIO fileio,
    IDiffer differ,
    IDelinearizer delinearizer
)
{
    public void Apply(string? basePath, string modPath, string? outputPath, bool alwaysOverride, bool delinearize)
    {
        var @base = fileio.OpenInput(ref basePath);
        var mod = fileio.OpenInput(ref modPath);
        var output = differ.Apply(@base, basePath, mod, modPath, alwaysOverride);

        if (output.Root!.HasElements)
        {
            if (delinearize)
                output = delinearizer.Apply(output, "temporary");
            fileio.SaveResult(outputPath, output);
        }
        else if (outputPath is not null && fileio.FileExists(outputPath))
            fileio.DeleteFile(outputPath);
    }
}
