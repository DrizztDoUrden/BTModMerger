﻿using Microsoft.Extensions.Logging;
using BTModMerger.Core.Interfaces;

using static BTModMerger.Core.BTMMSchema;

namespace BTModMerger.Tools;

public class LinearizerCLI(
    IFileIO fileio,
    ILinearizer linearizer
)
{
    public void Apply(string? inputPath, string? outputPath)
    {
        var input = fileio.OpenInput(ref inputPath);

        if (input.Root is null || input.Root.Name != Elements.Diff)
            throw new InvalidDataException($"({inputPath}) should be a BTMM diff xml.");

        var to = linearizer.Apply(input, inputPath);
        fileio.SaveResult(outputPath, to);
    }
}
