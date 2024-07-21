# BTModMerger

A tool to transform [Barotrauma](https://barotraumagame.com) mods into a diff-like format and reapply them to "base" files, including multiple in a sequence. This allows to automatically make compatibility patches for mods that override the same prefab. In theory, this can also be useful for versioning the mod source to automatically reapply it to newer versions of the same base file.

## Motivation

Why one may even want to turn a mod into a diff?

* Re-base a mod to another barotrauma version or a submod to another base mod version. To do that one can store a diff somewhere and on a new patch apply it again on a newer base file so that result would include base changes to irrelevant parts.
* Meaningfully store the mod XML files in git or another VCS. This would preserve history other VCS diffs from containing baseline barotrauma changes
* Fuse several mods that affect the same entity. Vanilla override system doesn't allow defining modification to part of an entity, so mods are inherently incompatible when they require changing different parts of the same one. Diffs allow that, so mods can be diffed, fused and reapplied. This would produce a patch-mod that may or may not require some additional changes depending on the original changes scope. I.e. mods that affect different parts of the same entity should mostly not require any additional changes.

In general, the purpose is to reduce amount of day-to-day routine manual XML-related labor required to develop and maintain a mod.

## Prerequisites

To build: [.Net SDK 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

To run: [.Net SDK 8 or .NET Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

To run self-contained verision: nothing, I guess.

Only tested on windows 11, but should build just fine or with minor modifications for linux as well.

## How to use:

```
> BTModMerger -?
> BTModMerger -help
```

Show usage description like this.

```
> BTModMerger Diff <base> <mod> [<output>] [-alwaysOverride] [-delinearize]
```

Transform a mod to moddiff format.

* base           - Path to the base file mod will be diffed from.
* mod            - Path to the mod file.
* output         - Path to a file to store result into. If not provided, defaults to base.moddiff. Optional.
* alwaysOverride - Interpret all elements as if they are inside of an override block. Somewhat useful for comparing mods. Optional.
* delinearize - Delinearize resulting diff. Optional, defaults to false.

```
 > BTModMerger Fuse [<output>] <parts> [-processCin] [-partsFromCin] [-delinearize] [-skipSimplifying] [-addNamespacePolicy] [-conflictHandlingPolicy] [<conflicts>] [-overrideConflicts] [-delinearizeConflicts]
 ```
 Fuse several diff XMLs into one file.

*   output                  - Path to a file to store result into. Optional. If not provided, cout would be used.
*    parts                   - Paths to files to fuse.
*    processCin              - Whether to interpret cin as an additional input. Optional.
*    partsFromCin            - Whether to interpret cin as list of part file names. Optional.
*    delinearize             - Delinearize resulting diff. Optional. [Default='False']
*    skipSimplifying         - Skip simplification after fusing. Optional. Fusion can produce duplicate or redundant elements, so simplification is advised.
*    addNamespacePolicy      - How to handle add: namespaces when simplifying. Optional, defaults to Remove. Incompatible with skipSimplifying
*    conflictHandlingPolicy  - How to handle conflicts like duplicate attribute updates. Optional, defaults to Override. Incompatible with skipSimplifying
*    conflicts               - Path to a file to generate conflict resolving diff into. If exists, content would not be wiped. Optional. If not provided, no file would be generated. Not very useful when conflict resolving is set to error. Incompatible with skipSimplifying
*    overrideConflicts       - Prevents from appending to whatever was in the conflicts file. Requiers conflicts generation enabled.
*    delinearizeConflicts    - Whether to delinearize conflicts file. Requiers conflicts generation enabled.

Add namespace policy values:

* Preserve - Leave all add: namespaces as they are.
* Add - Add add: namespaces wherever applicable
* Remove - Remove add: namespace everywhere

Conflict handling policy values:
* Override - Override conflicts with the value from later file
* Error - Produce an error on conflict

```
> BTModMerger Apply [<base>] <mod> [<output>] [-asOverride] [-delinearize] [-skipSimplifying]
```

Apply a mod in moddiff format to a base file. Can be done repeatedly.

* base            - Path to the base file mod will be applied to. Optional. If not provided, cin would be used.
* mod             - Path to the mod file.
* output          - Path to a file to store result into. Optional. If not provided, cout would be used.
* override        - Whether to generate a file with just overrides and additions (aka a mod) rather than all info. Optional, default: false.

```
> BTModMerger Indent [<input>] [<output>]
```

Indent an XML file.

* input    - Path to a file to indent. If not provided, cin would be used.
* output   - Path to a file to store result into. If not provided, cout would be used.

```
> BTModMerger Linearize [<input>] [<output>]
```
Linearize an XML diff file. Removes all into elements and moves their content to the root with appropriate btmm:Path attributes

* input  - Path to a file to indent. Optional. If not provided, cin would be used.
* output - Path to a file to store result into. Optional. If not provided, cout would be used.

```
> BTModMerger Delinearize [<input>] [<output>]
```
Delinearize an XML diff file. Makes all `btmm:Path` attributes contain exactly single element.

* input  - Path to a file to indent. Optional. If not provided, cin would be used.
* output - Path to a file to store result into. Optional. If not provided, cout would be used.

```
> BTModMerger Simplify [<input>] [<output>]
```

Simplify an XML diff file. Removes duplicates and empty elements.

* input  - Path to a file to indent. Optional. If not provided, cin would be used.
* output - Path to a file to store result into. Optional. If not provided, cout would be used.
*    addNamespacePolicy      - How to handle add: namespaces when simplifying. Optional, defaults to Remove. Incompatible with skipSimplifying
*    conflictHandlingPolicy  - How to handle conflicts like duplicate attribute updates. Optional, defaults to Override. Incompatible with skipSimplifying
*    conflicts               - Path to a file to generate conflict resolving diff into. If exists, content would not be wiped. Optional. If not provided, no file would be generated. Not very useful when conflict resolving is set to error. Incompatible with skipSimplifying
*    overrideConflicts       - Prevents from appending to whatever was in the conflicts file. Requiers conflicts generation enabled.
*    delinearizeConflicts    - Whether to delinearize conflicts file. Requiers conflicts generation enabled.

## Universal options, aka what can be added to any command

* PathToMetadata - Path to a metadata file. Defaults to: exe_dir/BTMetadata.xml. When unset would be generated if missing.

## Example of usage

To parse an entire mod one can just use something like the following script:

```pwsh
[CmdletBinding()]
Param(
    [String] $merger = "C:\git\BTModMerger\bin\Debug\net8.0\BTModMerger.exe",
    [String] $modRoot = "C:\Program Files (x86)\Steam\steamapps\workshop\content\602960\12345\Override\",
    [String] $baroContent = "C:\Program Files (x86)\Steam\steamapps\common\Barotrauma\Content",
    [String[]] $excludedModFiles = @(),
    [String] $reports = "c:\git\reports",
    [String] $difsLocation = "$reports\diffs",
    [String] $regenLocation = "$reports\regen",
    [String] $reportsLocation = "$reports\reports",
    [String] $fusedBase = "$reports\base.xml"
)

Import-Module SplitPipeline

$excludedModFiles += "filelist.xml"

$baseFiles = Get-ChildItem -Include *.xml -Recurse $baroContent -Exclude Vanilla.xml
$modFiles = Get-ChildItem -Include *.xml -Recurse $modRoot -Exclude $excludedModFiles

if (Test-Path $fusedBase) { Remove-Item $fusedBase }
$baseFiles | % FullName | & $merger Fuse -output $fusedBase -partsFromCin

if ($LASTEXITCODE -NE 0 -Or -Not (Test-Path $fusedBase))
{
    Write-Error `
"Failed to fuse base files`
>    `$baseFiles | $merger Fuse -output $fusedBase -partsFromCin"
    exit
}

$modFiles | Foreach-Object -Parallel `
{
    $file = $_
    $merger = $Using:merger
    $modRoot = $Using:modRoot
    $reports = $Using:reports
    $difsLocation = $Using:difsLocation
    $regenLocation = $Using:regenLocation
    $reportsLocation = $Using:reportsLocation
    $fusedBase = $Using:fusedBase

    $relative = Resolve-Path -Path $file -RelativeBasePath $modRoot -Relative

    if (Test-Path $difsLocation\$relative) { Remove-Item $difsLocation\$relative }
    if (Test-Path $regenLocation\$relative) { Remove-Item $regenLocation\$relative }
    if (Test-Path "$reportsLocation\$relative.txt") { Remove-Item "$reportsLocation\$relative.txt" }

    & $merger Diff $fusedBase $file $difsLocation\$relative -delinearize

    if ($LASTEXITCODE -NE 0)
    {
        Write-Error `
"Failed processing file:`
>    $merger Diff `"$fusedBase`" `"$file`" `"$difsLocation\$relative`" -delinearize"
        exit
    }

    if (Test-Path $difsLocation\$relative)
    {
        & $merger Apply $fusedBase $difsLocation\$relative $regenLocation\$relative -override

        if ($LASTEXITCODE -NE 0 -or -Not (Test-Path $regenLocation\$relative))
        {
            Write-Error `
"Failed processing file:`
>    $merger Diff `"$fusedBase`" `"$file`" `"$difsLocation\$relative`"`
>    $merger Apply `"$fusedBase`" `"$difsLocation\$relative`" `"$regenLocation\$relative`" -override"
            exit
        }
    }
    else
    {
        New-Item "$reportsLocation\$relative.txt" -Force | Out-Null
        "File is exact copy of vanilla" > "$reportsLocation\$relative.txt"
        Write-Host "File is exact copy of vanilla: $relative"
    }
}

```

This would take every xml file in the mod, diff it with base game, store the result in c:\diffs ($difsLocation value)
Than it would take each diff and reapply it to the fused base file, storing results to c:\regen ($regenLocation value)
This would simplify each XML in mod, removing overrides that are 100% copy from vanilla

## How to write your own moddiffs or read existing

Here is a description of [moddiff syntax](ModdiffSyntax.md)

## Caveats

This is mostly irrelevant, but sometimes it is impossible to automatically determine whether some xml node group is an indexed array, meaning it can have multiple entities with either no identifiers or duplicate ones. For such cases it is possible to add them to `BTMetadata.xml` file that is next to the executable. If it is missing any successful invocation of `Diff` or `Apply` would regenerate it as an empty file. Simple cases of indexed collections (ones you do not expect to have items removed by mods from) can have their child items added to `Indexed` collection there. The rest should have them added to `Tricky`. Otherwise, indexing of items may be incorrect when applying diffs after other diffs with items removed.
