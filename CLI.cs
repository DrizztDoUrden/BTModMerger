using System.Reflection;
using System.Xml.Linq;

using PowerArgs;

namespace BTModMerger;

[TabCompletion, ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
sealed internal class CLI
{
    public enum AddNamespacePolicy
    {
        [ArgDescription("Leave all add: namespaces as they are.")]
        Preserve,
        [ArgDescription("Add add: namespaces wherever applicable")]
        Add,
        [ArgDescription("Remove add: namespace everywhere")]
        Remove,
    }

    const AddNamespacePolicy defaultAddNamespacePolicy = AddNamespacePolicy.Remove;
    const string defaultAddNamespacePolicyString = nameof(AddNamespacePolicy.Remove);

    public enum ConflictHandlingPolicy
    {
        [ArgDescription("Override conflicts with the value from later file")]
        Override,
        [ArgDescription("Produce an error on conflict")]
        Error,
    }

    const ConflictHandlingPolicy defaultConflictHandlingPolicy = ConflictHandlingPolicy.Override;
    const string defaultConflictHandlingPolicyString = nameof(ConflictHandlingPolicy.Override);

    public record ConflictsFileInfo(FileInfo Path, bool Override, bool Delinearize);

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
        [ArgDescription("Path to a file to store result into. Optional. If not provided, defaults to cout.")]
        string? output,
        [ArgDefaultValue(false)]
        [ArgDescription("Interpret all elements as if they are inside of an override block. Somewhat useful for comparing mods. Optional.")]
        bool alwaysOverride,
        [ArgDefaultValue(false)]
        [ArgDescription("Delinearize resulting diff. Optional.")]
        bool delinearize)
    {
        BTMetadata.Path = PathToMetadata;
        Differ.Apply(@base, mod, output, alwaysOverride, delinearize);
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

    [ArgActionMethod]
    [ArgDescription("Fuse several diff XMLs into one file.")]
    public void Fuse(
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgExistingFile, ArgRequired(PromptIfMissing = true, IfNot = "partsFromCin")]
        [ArgDescription("Paths to files to fuse.")]
        string[] parts,
        [ArgDescription("Whether to interpret cin as an additional input. Optional.")]
        bool processCin,
        [ArgCantBeCombinedWith("processCin")]
        [ArgDescription("Whether to interpret cin as list of part file names. Optional.")]
        bool partsFromCin,
        [ArgDefaultValue(false)]
        [ArgDescription("Delinearize resulting diff. Optional.")]
        bool delinearize,
        [ArgDescription("Skip simplification after fusing. Optional. Fusion can produce duplicate or redundant elements, so simplification is advised.")]
        bool skipSimplifying,
        [ArgCantBeCombinedWith("skipSimplifying")]
        [ArgDescription("How to handle add: namespaces when simplifying. Optional, defaults to " + defaultAddNamespacePolicyString)]
        AddNamespacePolicy? addNamespacePolicy,
        [ArgCantBeCombinedWith("skipSimplifying")]
        [ArgDescription("How to handle conflicts like duplicate attribute updates. Optional, defaults to " + defaultConflictHandlingPolicyString)]
        ConflictHandlingPolicy? conflictHandlingPolicy,
        [ArgCantBeCombinedWith("skipSimplifying")]
        [ArgRequired(If = "overrideConflicts | delinearizeConflicts")]
        [ArgDescription("Path to a file to generate conflict resolving diff into. If exists, content would not be wiped. Optional. If not provided, no file would be generated. Not very useful when conflict resolving is set to error.")]
        string? conflicts,
        [ArgDescription("Prevents from appending to whatever was in the conflicts file.")]
        bool overrideConflicts,
        [ArgDescription("Whether to delinearize conflicts file.")]
        bool delinearizeConflicts)
    {
        BTMetadata.Path = PathToMetadata;
        var cin = processCin || partsFromCin ? Console.OpenStandardInput() : null;
        Fuser.Apply(parts ?? [], cin, partsFromCin, output, delinearize, skipSimplifying, new(
            addNamespacePolicy ?? defaultAddNamespacePolicy,
            conflictHandlingPolicy ?? defaultConflictHandlingPolicy
        ), 
        conflicts is not null ? new(new(conflicts), overrideConflicts, delinearizeConflicts) : null);
    }

    [ArgActionMethod]
    [ArgDescription("Indent an XML file. Useful to validate the result of diff->apply")]
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

        ToolBase.SaveResult(output, XDocument.Load(inputFile));

        if (inputFile is FileStream)
            inputFile.Dispose();
    }

    [ArgActionMethod]
    [ArgDescription("Linearize an XML diff file. Removes all btmm:Into elements and moves their content to the root with appropriate btmm:Path attributes")]
    public void Linearize(
        [ArgExistingFile]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output)
    {
        Linearizer.Apply(input, output);
    }

    [ArgActionMethod]
    [ArgDescription("Delinearize an XML diff file. Makes all btmm:Path attributes contain exactly single element by moving things to btmm:Into elements.")]
    public void Delinearize(
        [ArgExistingFile]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output)
    {
        Delinearizer.Apply(input, output);
    }

    [ArgActionMethod]
    [ArgDescription("Simplify an XML diff file. Removes duplicates and empty elements.")]
    public void Simplify(
        [ArgExistingFile]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgDefaultValue(defaultAddNamespacePolicy)]
        [ArgDescription("How to handle add: namespaces when simplifying. Optional.")]
        AddNamespacePolicy addNamespacePolicy,
        [ArgDefaultValue(defaultConflictHandlingPolicy)]
        [ArgDescription("How to handle add: namespaces when simplifying. Optional.")]
        ConflictHandlingPolicy conflictHandlingPolicy,
        [ArgRequired(If = "overrideConflicts | delinearizeConflicts")]
        [ArgDescription("Path to a file to generate conflict resolving diff into. If exists, content would not be wiped. Optional. If not provided, no file would be generated. Not very useful when conflict resolving is set to error. To force creating a new file add * to path start.")]
        string? conflicts,
        [ArgDescription("Prevents from appending to whatever was in the conflicts file")]
        bool overrideConflicts,
        [ArgDescription("Whether to delinearize conflicts file.")]
        bool delinearizeConflicts)
    {
        Simplifier.Apply(
            input,
            output,
            new(addNamespacePolicy, conflictHandlingPolicy),
            conflicts is not null ? new(new(conflicts), overrideConflicts, delinearizeConflicts) : null);
    }

    public static int Main(string[] args)
    {
#if !DEBUG
        try
        {
#endif
            Args.InvokeAction<CLI>(args);
            return 0;
#if !DEBUG
    }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return -1;
        }
#endif
    }
}
