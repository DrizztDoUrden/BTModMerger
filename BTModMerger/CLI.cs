using BTModMerger.Abstractions;
using BTModMerger.Core;
using BTModMerger.Core.Interfaces;
using BTModMerger.Core.LargeTools;
using BTModMerger.Core.Schema;
using BTModMerger.Core.Tools;
using BTModMerger.LargeTools;
using BTModMerger.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PowerArgs;

namespace BTModMerger;

[TabCompletion, ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
public sealed class CLI
{
    const AddNamespacePolicy defaultAddNamespacePolicy = AddNamespacePolicy.Remove;
    const string defaultAddNamespacePolicyString = nameof(AddNamespacePolicy.Remove);

    const ConflictHandlingPolicy defaultConflictHandlingPolicy = ConflictHandlingPolicy.Override;
    const string defaultConflictHandlingPolicyString = nameof(ConflictHandlingPolicy.Override);

    public class Services(string pathToMetadata) : IDisposable
    {
        public ServiceProvider Provider { get; } = new ServiceCollection()
            .AddLogging(lb => lb.AddConsole())

            .AddTransient<IFileIO, FileIO>()
            .AddTransient<BTMetadata, BTMetadata>(sp => BTMetadata.Load(pathToMetadata))

            .AddTransient<ILinearizer, Linearizer>()
            .AddTransient<IDelinearizer, Delinearizer>()
            .AddTransient<ISimplifier, Simplifier>()
            .AddTransient<IDiffer, Differ>()
            .AddTransient<IApplier, Applier>()
            .AddTransient<IFuser, Fuser>()

            .AddTransient<LinearizerCLI, LinearizerCLI>()
            .AddTransient<DelinearizerCLI, DelinearizerCLI>()
            .AddTransient<SimplifierCLI, SimplifierCLI>()
            .AddTransient<DifferCLI, DifferCLI>()
            .AddTransient<ApplierCLI, ApplierCLI>()
            .AddTransient<FuserCLI, FuserCLI>()

            .AddTransient<IContentPackageFuser, ContentPackageFuser>()
            .AddTransient<IModDiffer, ModDiffer>()
            .AddTransient<IModApplier, ModApplier>()

            .AddTransient<ContentPackageFuserCLI, ContentPackageFuserCLI>()
            .AddTransient<ModDifferCLI, ModDifferCLI>()
            .AddTransient<ModApplierCLI, ModApplierCLI>()

            .BuildServiceProvider();

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                    Provider.Dispose();

                m_disposed = true;
            }
        }

        private bool m_disposed;
    }

    [ArgExistingFile]
    [ArgDescription("Path to a metadata file. Defaults to: exe_dir/BTMetadata.xml. If unset would be generated if missing.")]
    public string PathToMetadata { get; set; } = Path.Combine(AppContext.BaseDirectory, "BTMetadata.xml");

    [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
    public bool Help { get; set; }

    [ArgActionMethod]
    [ArgDescription("Fuse a content package by element type into multiple files in target directory.")]
    public async Task FusePackage(
        [ArgExistingFile]
        [ArgDescription(@"Path a content package file like Barotrauma\Content\ContentPackages\Vanilla.xml. When omitted it is expected to be in cin.")]
        string? package,
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to a directory to store results into.")]
        string target,
        [ArgDescription(@"Path to the package root. Usually it means the game directory. Can be inferred from the path in package argument. When both are missing would try default steam installation location.")]
        string? packageRoot)
    {
        if (package is null && packageRoot is null)
        {
            var attempts = DriveInfo.GetDrives().Select(drive => $@"{drive.Name}Program Files (x86)\Steam\steamapps\common\Barotrauma");

            foreach (var attempt in attempts)
            {
                if (Directory.Exists(attempt))
                {
                    packageRoot = attempt;
                    break;
                }
            }

            if (package is null)
                throw new InvalidDataException("Barotrauma is installed in non-default location. Please provide package or packageRoot arguments.");
        }

        using var services = new Services(PathToMetadata);
        await services.Provider.GetRequiredService<ContentPackageFuserCLI>().Apply(package, target, packageRoot);
    }

    [ArgActionMethod]
    [ArgDescription("Diff a mod with a fused base package. Result is stored into target directory.")]
    public async Task DiffMod(
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription(@"Path a fused content package root (what has been passed to target argument of FusePackage) or its manifest file.")]
        string package,
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription(@"Path a mod root or its filelist.xml file.")]
        string mod,
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to a directory to store results into.")]
        string target,
        [ArgDefaultValue(true)]
        [ArgDescription("Whether to generate files with just overrides and additions (aka a mod) rather than all info.")]
        bool @override)
    {
        using var services = new Services(PathToMetadata);
        await services.Provider.GetRequiredService<ModDifferCLI>().Apply(package, mod, target, @override);
    }

    [ArgActionMethod]
    [ArgDescription("Apply a mod to a fused base package. Result is stored into target directory.")]
    public async Task ApplyMod(
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription(@"Path a fused content package root (what has been passed to target argument of FusePackage) or its manifest file.")]
        string package,
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription(@"Path a mod diff root or its ModDiff.xml file.")]
        string diff,
        [ArgRequired(PromptIfMissing = true)]
        [ArgDescription("Path to a directory to store results into.")]
        string target)
    {
        using var services = new Services(PathToMetadata);
        await services.Provider.GetRequiredService<ModApplierCLI>().Apply(package, diff, target);
    }

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
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<DifferCLI>().Apply(@base, mod, output, alwaysOverride, delinearize);
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
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<ApplierCLI>().Apply(@base, mod, output, @override);
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
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<FuserCLI>().Apply(parts ?? [], processCin, partsFromCin, output, delinearize, skipSimplifying, new(
            addNamespacePolicy ?? defaultAddNamespacePolicy,
            conflictHandlingPolicy ?? defaultConflictHandlingPolicy
        ),
        conflicts is not null ? new(new(conflicts), overrideConflicts, delinearizeConflicts) : null);
    }

    [ArgActionMethod]
    [ArgDescription("Indent an XML file. Useful to validate the result of diff->apply")]
    public void Indent(
        [ArgExistingFile]
        [ArgRequired(If = "inPlace")]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgCantBeCombinedWith("output")]
        [ArgDescription("Whether to process input file inplace.")]
        bool inPlace)
    {

        using var services = new Services(PathToMetadata);
        var fileio = services.Provider.GetRequiredService<IFileIO>();
        fileio.SaveResult(inPlace ? input : output, fileio.OpenInput(ref input));
    }

    [ArgActionMethod]
    [ArgDescription("Linearize an XML diff file. Removes all btmm:Into elements and moves their content to the root with appropriate btmm:Path attributes")]
    public void Linearize(
        [ArgExistingFile]
        [ArgRequired(If = "inPlace")]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgCantBeCombinedWith("output")]
        [ArgDescription("Whether to process input file inplace.")]
        bool inPlace)
    {
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<LinearizerCLI>().Apply(input, output, inPlace);
    }

    [ArgActionMethod]
    [ArgDescription("Delinearize an XML diff file. Makes all btmm:Path attributes contain exactly single element by moving things to btmm:Into elements.")]
    public void Delinearize(
        [ArgExistingFile]
        [ArgRequired(If = "inPlace")]
        [ArgDescription("Path to a file to indent. Optional. If not provided, cin would be used.")]
        string? input,
        [ArgDescription("Path to a file to store result into. Optional. If not provided, cout would be used.")]
        string? output,
        [ArgCantBeCombinedWith("output")]
        [ArgDescription("Whether to process input file inplace.")]
        bool inPlace)
    {
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<DelinearizerCLI>().Apply(input, output, inPlace);
    }

    [ArgActionMethod]
    [ArgDescription("Simplify an XML diff file. Removes duplicates and empty elements.")]
    public void Simplify(
        [ArgExistingFile]
        [ArgRequired(If = "inPlace")]
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
        bool delinearizeConflicts,
        [ArgCantBeCombinedWith("output")]
        [ArgDescription("Whether to process input file inplace.")]
        bool inPlace)
    {
        using var services = new Services(PathToMetadata);
        services.Provider.GetRequiredService<SimplifierCLI>().Apply(
            input,
            output,
            new(addNamespacePolicy, conflictHandlingPolicy),
            conflicts is not null ? new(new(conflicts), overrideConflicts, delinearizeConflicts) : null,
            inPlace);
    }

    public static async Task<int> Main(string[] args)
    {
#if !DEBUG
        try
        {
#endif
        await Args.InvokeActionAsync<CLI>(args);
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
