using System.Reflection;
using System.Xml.Linq;

using PowerArgs;

namespace BTModMerger;

[TabCompletion, ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
sealed internal class CLI
{
    [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
    public bool Help { get; set; }

    [ArgExistingFile]
    [ArgDescription("Path to a metadata file. Defaults to: exe_dir/BTMetadata.xml. If unset would be generated if missing.")]
    public string PathToMetadata { get; set; } = Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!.FullName, "BTMetadata.xml");

    [ArgActionMethod, ArgDescription("Transform a mod to moddiff format")]
    public void Diff(
        [ArgExistingFile, ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to a base file mod will be diffed from.")]
        string @base,
        [ArgExistingFile, ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to the mod file.")]
        string mod,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, defaults to <base>.moddiff.")]
        string? output)
    {
        BTMetadata.Path = PathToMetadata;
        Differ.Apply(@base, mod, output ?? $"{mod}.diff");
    }

    [ArgActionMethod]
    [ArgDescription("Apply a mod in moddiff format into a base file. Can be done repeatedly.")]
    public void Apply(
        [ArgExistingFile]
        [ArgDescription("Path to a base file mod will be applied to. Optional. If not provided, cin would be used.")]
        string? @base,
        [ArgExistingFile, ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to the mod file.")]
        string mod,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output)
    {
        BTMetadata.Path = PathToMetadata;

        using var baseFile = string.IsNullOrWhiteSpace(@base)
            ? Console.OpenStandardInput()
            : File.OpenRead(@base);

        if (!string.IsNullOrEmpty(output))
        {
            var containingDir = new FileInfo(output).Directory;
            if (!containingDir!.Exists)
                containingDir.Create();
        }

        using var outputFile = string.IsNullOrWhiteSpace(output)
            ? Console.OpenStandardOutput()
            : File.Create(output);

        Merger.Apply(baseFile, mod, outputFile);
    }

    [ArgActionMethod, ArgDescription("Indent an XML file. Useful to validate the result of diff->apply")]
    public void Indent(
        [ArgExistingFile]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output)
    {
        using var inputFile = string.IsNullOrWhiteSpace(input)
            ? Console.OpenStandardInput()
            : File.OpenRead(input);

        if (!string.IsNullOrEmpty(output))
        {
            var containingDir = new FileInfo(output).Directory;
            if (!containingDir!.Exists)
                containingDir.Create();
        }

        using var outputFile = string.IsNullOrWhiteSpace(output)
            ? Console.OpenStandardOutput()
            : File.Create(output);

        XDocument.Load(inputFile).Save(outputFile);
    }

    public static void Main(string[] args) => Args.InvokeAction<CLI>(args);
}
