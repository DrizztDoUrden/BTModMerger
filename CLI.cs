using System.Reflection;
using System.Xml;
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
        [ArgExistingFile]
        [ArgDescription("Path to a base file mod will be diffed from. Optional. If not provided, cin would be used.")]
        string? @base,
        [ArgExistingFile, ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to the mod file.")]
        string mod,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, defaults to <base>.moddiff.")]
        string? output,
        [ArgDefaultValue(false)]
        [ArgDescription("Interpret all elements as if they are inside of an override block. Somewhat useful for comparing mods. Optional.")]
        bool alwaysOverride)
    {
        BTMetadata.Path = PathToMetadata;
        Differ.Apply(@base, mod, output ?? $"{mod}.diff", alwaysOverride);
    }

    [ArgActionMethod]
    [ArgDescription("Apply a mod in moddiff format to a base file. Can be done repeatedly.")]
    public void Apply(
        [ArgExistingFile]
        [ArgDescription("Path to a base file mod will be applied to. Optional. If not provided, cin would be used.")]
        string? @base,
        [ArgExistingFile, ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to the mod file.")]
        string mod,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgDefaultValue(false)]
        [ArgDescription("Whether to generate a file with just overrides and additions (aka a mod) rather than all info. Optional.")]
        bool @override)
    {
        BTMetadata.Path = PathToMetadata;

        Aplyier.Apply(@base, mod, output, @override);
    }

    [ArgActionMethod, ArgDescription("Indent an XML file. Useful to validate the result of diff->apply")]
    public void Indent(
        [ArgExistingFile]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output)
    {
        var inputFile = string.IsNullOrWhiteSpace(input)
            ? Console.OpenStandardInput()
            : File.OpenRead(input);

        if (!string.IsNullOrEmpty(output))
        {
            var containingDir = new FileInfo(output).Directory;
            if (!containingDir!.Exists)
                containingDir.Create();
        }

        var outputFile = string.IsNullOrWhiteSpace(output)
            ? Console.OpenStandardOutput()
            : File.Create(output);

        using (var writer = XmlWriter.Create(outputFile, BTMMSchema.WriterSettings))
            XDocument.Load(inputFile).Save(writer);

        if (inputFile is FileStream)
            inputFile.Dispose();
        if (outputFile is FileStream)
            outputFile.Dispose();
    }

    public static void Main(string[] args) => Args.InvokeAction<CLI>(args);
}
